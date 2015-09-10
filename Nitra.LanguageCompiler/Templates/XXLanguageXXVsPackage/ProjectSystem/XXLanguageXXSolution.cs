using Nitra.Declarations;
using Nitra.ProjectSystem;
using Nitra.VisualStudio;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using JetBrains.Application.changes;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;

namespace XXNamespaceXX.ProjectSystem
{
  public class XXLanguageXXSolution : Solution, INitraSolutionService
  {
    public bool IsOpened { get; private set; }

    private ISolution _solution;
    private readonly Dictionary<IProject, XXLanguageXXProject> _projectsMap = new Dictionary<IProject, XXLanguageXXProject>();
    private readonly Dictionary<string, Action<File>> _fileOpenNotifyRequest = new Dictionary<string, Action<File>>();

    public XXLanguageXXSolution()
    {
    }

    public void Open(Lifetime lifetime, ChangeManager changeManager, ISolution solution)
    {
      Debug.Assert(!IsOpened);

      _solution = solution;
      changeManager.Changed2.Advise(lifetime, Handler);
      lifetime.AddAction(Close);
      _fileOpenNotifyRequest.Clear();
      IsOpened = true;
    }

    private void Close()
    {
      IsOpened = false;
      foreach (var project in _projectsMap.Values)
        project.Dispose();
      _projectsMap.Clear();
      _solution = null;
    }

    public override IEnumerable<Project> Projects { get { return _projectsMap.Values; } }

    private void Handler(ChangeEventArgs changeEventArgs)
    {
      var projectModelChange = changeEventArgs.ChangeMap.GetChange<ProjectModelChange>(_solution);
      if (projectModelChange != null)
      {
        projectModelChange.Accept(new RecursiveProjectModelChangeDeltaVisitor(FWithDelta, FWithItemDelta));
      }
    }

    private void FWithDelta(ProjectModelChange obj) { }

    private void FWithItemDelta(ProjectItemChange obj)
    {
      var item = obj.ProjectItem;

      var file = item as IProjectFile;
      if (file != null && file.LanguageType.Is<XXLanguageXXFileType>())
      {
        if (obj.IsRemoved)
        {
          var project = GetProject(obj.OldParentFolder.GetProject());
          project.TryRemoveFile(file);
        }
        else if (obj.IsAdded)
        {
          var project = GetProject(file.GetProject());
          var nitraFile = project.TryAddFile(file);
          Action<File> oldHandler;
          if (_fileOpenNotifyRequest.TryGetValue(nitraFile.FullName, out oldHandler))
            oldHandler(nitraFile);
        }
      }
    }

    private XXLanguageXXProject GetProject(IProject project)
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
        }

        return;
      }

      Action<File> oldHandler;
      if (_fileOpenNotifyRequest.TryGetValue(filePath, out oldHandler))
        _fileOpenNotifyRequest[filePath] = oldHandler + handler;
      else
        _fileOpenNotifyRequest.Add(filePath, handler);
    }
  }
}
