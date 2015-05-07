using JetBrains.Application;
using JetBrains.Application.changes;
using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Data.Core;

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace XXNamespaceXX
{
  [SolutionComponent]
  public class SolutionStart
  {
    private readonly ISolution _solution;
    private Dictionary<IProject, List<IProjectFile>> _projectsMap = new Dictionary<IProject, List<IProjectFile>>();

    public SolutionStart(Lifetime lifetime, ChangeManager changeManager, ISolution solution)
    {
      _projectsMap.Clear();
      _solution = solution;
      changeManager.Changed2.Advise(lifetime, Handler);
      lifetime.AddAction(_projectsMap.Clear);
    }

    private void Handler(ChangeEventArgs changeEventArgs)
    {
      var projectModelChange = changeEventArgs.ChangeMap.GetChange<ProjectModelChange>(_solution);
      if (projectModelChange != null)
      {
        //Debug.WriteLine(projectModelChange.Dump());
        //Do(projectModelChange);
        projectModelChange.Accept(new RecursiveProjectModelChangeDeltaVisitor(FWithDelta, FWithItemDelta));
      }
    }

    private void FWithItemDelta(ProjectItemChange obj)
    {
      //Debug.WriteLine(obj.Dump());
      var item = obj.ProjectItem;

      var file = item as IProjectFile;
      if (file != null && file.LanguageType.Is<XXLanguageXXFileType>())
      {
        if (obj.IsRemoved)
        {
          var project = obj.OldParentFolder.GetProject();
          var files = GetProjectFiles(project);
          files.Add(file);
        }
        else if (obj.IsAdded)
        {
          var project = file.GetProject();
          var files = GetProjectFiles(project);
          files.Add(file);
        }
      }
    }

    private void FWithDelta(ProjectModelChange obj)
    {
    }

    private List<IProjectFile> GetProjectFiles(IProject project)
    {
      Debug.Assert(project != null);

      List<IProjectFile> files;

      if (_projectsMap.TryGetValue(project, out files))
        return files;

      files = new List<IProjectFile>();
      _projectsMap.Add(project, files);
      return files;
    }
  }

  [ZoneMarker]
  public class ZoneMarker
  {
  }
}
