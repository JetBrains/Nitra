using Nitra.Declarations;
using Nitra.ProjectSystem;

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
  public class XXLanguageXXSolution : Solution
  {
    private readonly ISolution _solution;
    private readonly Dictionary<IProject, XXLanguageXXProject> _projectsMap = new Dictionary<IProject, XXLanguageXXProject>();

    public XXLanguageXXSolution(Lifetime lifetime, ChangeManager changeManager, ISolution solution)
    {
      _solution = solution;
      changeManager.Changed2.Advise(lifetime, Handler);
      lifetime.AddAction(Close);
    }

    private void Close()
    {
      foreach (var project in _projectsMap.Values)
        project.Dispose();
      _projectsMap.Clear();
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
          project.TryAddFile(file);
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
  }
}
