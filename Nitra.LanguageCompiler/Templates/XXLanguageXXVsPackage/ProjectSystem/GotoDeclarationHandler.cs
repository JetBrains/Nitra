using Nitra.Declarations;
using Nitra.ProjectSystem;
using Nitra.Runtime.Binding;
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
using System.Linq;

namespace XXNamespaceXX.ProjectSystem
{
class GotoDeclarationHandler : IExecutableAction
  {
    [NotNull] private readonly ICommandProcessor            _commandProcessor;
    [NotNull] private readonly TextControlChangeUnitFactory _changeUnitFactory;
    [NotNull] private readonly XXLanguageXXSolution         _nitraSolution;

    public GotoDeclarationHandler(ICommandProcessor commandProcessor, TextControlChangeUnitFactory changeUnitFactory, XXLanguageXXSolution nitraSolution)
    {
      _commandProcessor  = commandProcessor;
      _changeUnitFactory = changeUnitFactory;
      _nitraSolution     = nitraSolution;
    }

    /*
    IActionRequirement IActionWithExecuteRequirement.GetRequirement(IDataContext dataContext)
    {
      if (!FastCheckAvailable(dataContext))
        return EmptyRequirement.Instance;
      return CurrentPsiFileRequirement.FromDataContext(dataContext);
    }
    private bool FastCheckAvailable(IDataContext dataContext)
    {
      var solution = dataContext.GetData(ProjectModel.DataContext.DataConstants.SOLUTION);
      if (solution == null) return false;
      var textControl = dataContext.GetData(TextControl.DataContext.DataConstants.TEXT_CONTROL);
      if (textControl == null) return false;
      return TODO;
    }
    */

    public bool Update(IDataContext dataContext, ActionPresentation presentation, DelegateUpdate nextUpdate)
    {
      var solution = dataContext.GetData(JetBrains.ProjectModel.DataContext.DataConstants.SOLUTION);
      if (solution == null)
        return nextUpdate();

      var textControl = dataContext.GetData(JetBrains.TextControl.DataContext.DataConstants.TEXT_CONTROL);
      if (textControl == null)
        return nextUpdate();

      return IsAvailableOrExecuteEww(solution, textControl, execute: false) || nextUpdate();
    }

    public void Execute(IDataContext context, DelegateExecute nextExecute)
    {
      var solution = context.GetData(JetBrains.ProjectModel.DataContext.DataConstants.SOLUTION);
      if (solution != null)
      {
        var doc = context.GetData(JetBrains.DocumentModel.DataConstants.DOCUMENT);
        var documentOffset = context.GetData(JetBrains.DocumentModel.DataConstants.DOCUMENT_OFFSET);
        if (doc == null || documentOffset == null)
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
        var visitor = new CollectSymbolRefsAstVisitor(new NSpan(pos));
        nitraFile.Ast.Accept(visitor);

      }

      nextExecute();
    }

    private bool IsAvailableOrExecuteEww([NotNull] ISolution solution, [NotNull] ITextControl textControl, bool execute)
    {
      return true; // TODO: check for Nitra file
    }
  }
}
