using Nitra.Declarations;
using Nitra.ProjectSystem;
using Nitra.VisualStudio;
using Nitra.VisualStudio.Coloring;

using JetBrains.ActionManagement;
using JetBrains.Annotations;
using JetBrains.Application.changes;
using JetBrains.Application.CommandProcessing;
using JetBrains.Application.DataContext;
using JetBrains.DataFlow;
using JetBrains.DocumentManagers;
using JetBrains.DocumentManagers.impl;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.LiveTemplates;
using JetBrains.ReSharper.Feature.Services.Lookup;
using JetBrains.ReSharper.Feature.Services.Navigation.ContextNavigation;
using JetBrains.ReSharper.Feature.Services.Util;
using JetBrains.ReSharper.Features.Intellisense.CodeCompletion.CSharp.Rules.SourceTemplates;
using JetBrains.ReSharper.Features.Navigation.Features.GoToDeclaration;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Files;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.TextControl;
using JetBrains.TextControl.Util;
using JetBrains.UI.ActionsRevised;
using JetBrains.UI.ActionSystem.Text;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using JetBrains.Application;
using JetBrains.CommonControls;
using JetBrains.Metadata.Reader.API;
using JetBrains.ReSharper.Feature.Services.Navigation.NavigationExtensions;
using JetBrains.ReSharper.Feature.Services.Occurences;
using JetBrains.ReSharper.Feature.Services.Presentation;
using JetBrains.ReSharper.Feature.Services.Tree;
using JetBrains.TreeModels;
using JetBrains.UI.PopupMenu;
using JetBrains.UI.PopupWindowManager;
using JetBrains.UI.RichText;
using JetBrains.Util;
using JetBrains.Util.Special;

using Path = System.IO.Path;

namespace XXNamespaceXX.ProjectSystem
{
  public partial class XXLanguageXXSolution : Solution, INitraSolutionService
  {
    private class FindUsagesHandler : IExecutableAction
    {
      [NotNull]
      private readonly ICommandProcessor _commandProcessor;
      [NotNull]
      private readonly TextControlChangeUnitFactory _changeUnitFactory;
      [NotNull]
      private readonly XXLanguageXXSolution _nitraSolution;
      [NotNull]
      private readonly Lifetime _lifetime;
      private readonly IShellLocks _shellLocks;

      public FindUsagesHandler(Lifetime lifetime, IShellLocks shellLocks, ICommandProcessor commandProcessor, TextControlChangeUnitFactory changeUnitFactory, XXLanguageXXSolution nitraSolution)
      {
        _lifetime = lifetime;
        _shellLocks = shellLocks;
        _commandProcessor = commandProcessor;
        _changeUnitFactory = changeUnitFactory;
        _nitraSolution = nitraSolution;
      }

      public bool Update(IDataContext dataContext, ActionPresentation presentation, DelegateUpdate nextUpdate)
      {
        var solution = dataContext.GetData(JetBrains.ProjectModel.DataContext.DataConstants.SOLUTION);
        if (solution == null)
          return nextUpdate();

        var textControl = dataContext.GetData(JetBrains.TextControl.DataContext.DataConstants.TEXT_CONTROL);
        if (textControl == null)
          return nextUpdate();

        var doc = dataContext.GetData(JetBrains.DocumentModel.DataConstants.DOCUMENT);
        if (doc == null)
          return nextUpdate();

        return true;
      }

      public void Execute(IDataContext context, DelegateExecute nextExecute)
      {
        var solution = context.GetData(JetBrains.ProjectModel.DataContext.DataConstants.SOLUTION);
        if (solution != null)
        {
          var documentOffset = context.GetData(JetBrains.DocumentModel.DataConstants.DOCUMENT_OFFSET);
          if (documentOffset == null)
            return;

          //var psiServices = solution.GetPsiServices();
          var psiFile = documentOffset.Document.GetPsiSourceFile(solution);
          if (psiFile == null)
            return;
          var projectFile = psiFile.ToProjectFile();
          if (projectFile == null)
            return;
          var project = projectFile.GetProject();
          if (project == null)
            return;
          var nitraProject = _nitraSolution.GetProject(project);
          if (nitraProject == null)
            return;
          var nitraFile = nitraProject.TryGetFile(projectFile);
          if (nitraFile == null)
            return;

          var pos = documentOffset.Value;
          var symbolCollector = new CollectSymbolsAndRefsInSpanAstVisitor(new NSpan(pos));
          nitraFile.Ast.Accept(symbolCollector);


          var popupWindowContext = context.GetData(JetBrains.UI.DataConstants.PopupWindowContextSource);
          if (popupWindowContext == null)
            return;

          var symbols = symbolCollector.Refs.Where(r => r.IsSymbolEvaluated).Select(r => r.Symbol).ToArray();

          if (symbols.Length == 0)
          {
            if (symbolCollector.Names.Count == 0)
              return;

            symbols = symbolCollector.Names.Where(n => n.IsSymbolEvaluated).Select(n => n.Symbol).ToArray();
          }

          List<IOccurence> items = new List<IOccurence>();
          var s = nitraFile.Project.Solution;

          foreach (var p in s.Projects)
          {
            foreach (var file in p.Files)
            {

              foreach (var symbol in symbols)
              {
                var collectRefs = new CollectSymbolRefsAstVisitor(symbol);
                file.Ast.Accept(collectRefs);

                foreach (var r in collectRefs.FoundSymbols)
                {
                  var refNitraFile = r.File as XXLanguageXXFile; // TODO: add INitraReSharperFile
                  if (refNitraFile == null)
                    continue;
                  items.Add(new RangeOccurence(refNitraFile.PsiSourceFile, new DocumentRange(refNitraFile.Document, new TextRange(r.Span.StartPos, r.Span.EndPos))));
                }
              }
            }
          }

          var descriptor = new NitraOccurenceBrowserDescriptor(solution, items);
          FindResultsBrowser.ShowResults(descriptor);
        }
        nextExecute();
      }

      private class NitraOccurenceBrowserDescriptor : OccurenceBrowserDescriptor
      {
        public NitraOccurenceBrowserDescriptor([NotNull] ISolution solution, ICollection<IOccurence> items) : base(solution)
        {
          Title.Value = "Nitra ...";
          SetResults(items);
        }

        public override TreeModel Model
        {
          get
          {
            return OccurenceSections.Select(section => section.Model).FirstOrDefault();
          }
        }
      }

      private IEnumerable<Declaration> Sorter(IEnumerable<Declaration> decls)
      {
        return decls.OrderBy(d => d.Name.Text).ThenBy(d => d.File.FullName).ThenBy(d => d.Name.Span.StartPos);
      }

      private static void Navigate(Declaration decl, ISolution solution, IProject project, PopupWindowContextSource popupWindowContext)
      {
        var nitraSymbolFile = decl.File;
        var symbolProjectFile = GrtProjectFile(project, decl.File);

        if (symbolProjectFile != null)
          symbolProjectFile = GrtSolutionFile(solution, decl.File);

        if (symbolProjectFile != null)
        {
          var r = new ProjectFileTextRange(symbolProjectFile, decl.Name.Span.StartPos, TargetFrameworkId.Default);
          r.Navigate(popupWindowContext, true);
          return;
        }
      }

      private static IProjectFile GrtProjectFile(IProject project, File nitraSymbolFile)
      {
        var projectItems = project.FindProjectItemsByLocation(FileSystemPath.Parse(nitraSymbolFile.FullName));
        foreach (var projectItem in projectItems)
        {
          var symbolProjectFile = projectItem as IProjectFile;
          if (symbolProjectFile != null)
            return symbolProjectFile;
        }

        return null;
      }

      private static IProjectFile GrtSolutionFile(ISolution solution, File nitraSymbolFile)
      {
        var projectItems = solution.FindProjectItemsByLocation(FileSystemPath.Parse(nitraSymbolFile.FullName));
        foreach (var projectItem in projectItems)
        {
          var symbolProjectFile = projectItem as IProjectFile;
          if (symbolProjectFile != null)
            return symbolProjectFile;
        }

        return null;
      }
    }
  }
}
