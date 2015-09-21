using Nitra.Declarations;
using Nitra.ProjectSystem;
using Nitra.VisualStudio;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
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

namespace XXNamespaceXX.ProjectSystem
{
  public class XXLanguageXXSolution : Solution, INitraSolutionService
  {
    public bool IsOpened { get; private set; }

    private ISolution _solution;
    private readonly Dictionary<IProject, XXLanguageXXProject> _projectsMap = new Dictionary<IProject, XXLanguageXXProject>();
    private readonly Dictionary<string, Action<File>> _fileOpenNotifyRequest = new Dictionary<string, Action<File>>(StringComparer.OrdinalIgnoreCase);
    private IActionManager _actionManager;

    private DocumentManager _documentManager;

    public XXLanguageXXSolution()
    {
    }

    public void Open(
      Lifetime lifetime, 
      ChangeManager changeManager,
      ISolution solution,
      DocumentManager documentManager,
      IActionManager actionManager,
      ICommandProcessor commandProcessor,
      TextControlChangeUnitFactory changeUnitFactory)
    {
      Debug.Assert(!IsOpened);

      _solution = solution;
      _documentManager = documentManager;
      changeManager.Changed2.Advise(lifetime, Handler);
      lifetime.AddAction(Close);
      var expandAction = actionManager.Defs.TryGetActionDefById(GotoDeclarationAction.ACTION_ID);
      if (expandAction != null)
      {
        var postfixHandler = new GotoDeclarationHandler(commandProcessor, changeUnitFactory, this);

        lifetime.AddBracket(
          FOpening: () => actionManager.Handlers.AddHandler(expandAction, postfixHandler),
          FClosing: () => actionManager.Handlers.RemoveHandler(expandAction, postfixHandler));
      }
    }

    private void Close()
    {
      IsOpened = false;
      foreach (var project in _projectsMap.Values)
        project.Dispose();
      _projectsMap.Clear();
      _solution = null;
      _fileOpenNotifyRequest.Clear();
      _documentManager = null;
    }

    public override IEnumerable<Project> Projects { get { return _projectsMap.Values; } }

    private void Handler(ChangeEventArgs changeEventArgs)
    {
      var projectModelChange = changeEventArgs.ChangeMap.GetChange<ProjectModelChange>(_solution);
      if (projectModelChange != null)
      {
        if (projectModelChange.ContainsChangeType(ProjectModelChangeType.PROJECT_MODEL_CACHES_READY))
        {
          foreach (var project in _projectsMap.Values)
            project.UpdateProperties();
          IsOpened = true;
        }

        projectModelChange.Accept(new RecursiveProjectModelChangeDeltaVisitor(FWithDelta, FWithItemDelta));
      }

      {
        var documentChange = changeEventArgs.ChangeMap.GetChange<ProjectFileDocumentChange>(_documentManager.ChangeProvider);
        if (documentChange != null)
          if (OnFileChanged(documentChange.ProjectFile, documentChange))
            return;
      }

      {
        var documentChange = changeEventArgs.ChangeMap.GetChange<DocumentChange>(_documentManager.ChangeProvider);
        if (documentChange != null)
          if (OnFileChanged(_documentManager.GetProjectFile(documentChange.Document), documentChange))
            return;
      }
    }

    private bool OnFileChanged(IProjectFile projectFile, DocumentChange documentChange)
    {
      var project = projectFile.GetProject();
      if (project != null)
      {
        XXLanguageXXProject nitraProject;
        if (_projectsMap.TryGetValue(project, out nitraProject))
        {
          var nitraFile = nitraProject.TryGetFile(projectFile);
          if (nitraFile != null)
          {
            nitraFile.OnFileChanged(documentChange);
            return true;
          }
        }
      }

      return false;
    }

    private void FWithDelta(ProjectModelChange obj) { }

    private void FWithItemDelta(ProjectItemChange obj)
    {
      var item = obj.ProjectItem;

      var file = item as IProjectFile;
      if (file != null && file.LanguageType.Is<XXLanguageXXFileType>())
      {
        if (obj.IsRemoved || obj.IsMovedOut)
        {
          var project = GetProject(obj.OldParentFolder.GetProject());
          project.TryRemoveFile(file);
        }
        else if (obj.IsAdded || obj.IsMovedIn)
        {
          var project = GetProject(file.GetProject());
          var sourceFile = file.ToSourceFile();
          if (sourceFile == null)
            return;

          var nitraFile = project.TryAddFile(file);
          Action<File> oldHandler;
          if (_fileOpenNotifyRequest.TryGetValue(nitraFile.FullName, out oldHandler))
            oldHandler(nitraFile);
        }
      }
    }

    public XXLanguageXXProject GetProject(IProject project)
    {
      XXLanguageXXProject result;
      if (_projectsMap.TryGetValue(project, out result))
        return result;

      result = new XXLanguageXXProject(project);
      
      _projectsMap.Add(project, result);

      return result;
    }

    /// <summary>
    /// INitraSolutionService.NotifiWhenFileIsOpened implementation.
    /// </summary>
    public void NotifiWhenFileIsOpened(string filePath, Action<File> handler)
    {
      if (IsOpened)
      {
        foreach (var project in _projectsMap.Values)
        {
          var file = project.TryGetFile(filePath);
          if (file == null)
            continue;

          handler(file);
          return;
        }
      }

      Action<File> oldHandler;
      if (_fileOpenNotifyRequest.TryGetValue(filePath, out oldHandler))
        _fileOpenNotifyRequest[filePath] = oldHandler + handler;
      else
        _fileOpenNotifyRequest.Add(filePath, handler);
    }
  }
}
