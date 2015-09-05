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
using Nitra.CSharp;

namespace XXNamespaceXX.ProjectSystem
{
  public class XXLanguageXXSolution : Solution
  {
    private readonly ISolution _solution;
    private readonly Dictionary<IProject, XXLanguageXProject> _projectsMap = new Dictionary<IProject, XXLanguageXProject>();

    //public ReadOnlyObservableCollection<XXLanguageXProject> XXLanguageXProjects { get; private set; }

    public XXLanguageXXSolution(Lifetime lifetime, ChangeManager changeManager, ISolution solution)
    {
      _solution = solution;
      changeManager.Changed2.Advise(lifetime, Handler);
      lifetime.AddAction(_projectsMap.Clear);
    }

      public override IEnumerable<Project> Projects { get { return _projectsMap.Values; } }

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

    private void FWithDelta(ProjectModelChange obj) { }

    private void FWithItemDelta(ProjectItemChange obj)
    {
      //Debug.WriteLine(obj.Dump());
      var item = obj.ProjectItem;

      var file = item as IProjectFile;
      if (file != null && file.LanguageType.Is<NitraCSharpFileType>())
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

    private XXLanguageXProject GetProject(IProject project)
    {
      XXLanguageXProject result;
      if (_projectsMap.TryGetValue(project, out result))
        return result;

      result = new XXLanguageXProject(project);
      
      _projectsMap.Add(project, result);
      //XXLanguageXProjects.Add(result);

      return result;
    }
  }
}
