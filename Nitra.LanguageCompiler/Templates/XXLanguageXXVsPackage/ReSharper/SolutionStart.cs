using JetBrains.Application;
using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.ProjectModel;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Data.Core;

using System;
using System.Diagnostics;
using JetBrains.Application.changes;
using JetBrains.DataFlow;

namespace XXNamespaceXX
{
  [SolutionComponent]
  public class SolutionStart
  {
    private readonly ISolution _solution;

    public SolutionStart(Lifetime lifetime, ChangeManager changeManager, ISolution solution)
    {
      _solution = solution;
      changeManager.Changed2.Advise(lifetime, Handler);
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
          Debug.WriteLine(file.LanguageType + ": " + item.Location + "  removed from: " + project);
        }
        else if (obj.IsAdded)
        {
          var project = file.GetProject();
          Debug.WriteLine(file.LanguageType + ": " + item.Location + "  in: " + project);
        }
      }
    }

    private void FWithDelta(ProjectModelChange obj)
    {
    }
  }

  [ZoneMarker]
  public class ZoneMarker
  {
  }
}
