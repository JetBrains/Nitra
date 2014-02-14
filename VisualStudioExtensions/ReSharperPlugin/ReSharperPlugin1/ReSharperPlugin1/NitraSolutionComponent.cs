using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.DataFlow;
using JetBrains.DocumentManagers;
using JetBrains.DocumentManagers.impl;
using JetBrains.ProjectModel;
using System.Diagnostics;

namespace ReSharperPlugin1
{
  [SolutionComponent]
  class NitraSolutionComponent : IChangeProvider
  {
    private ISolution _solution;
    private DocumentManager _documentManager;

    public NitraSolutionComponent(Lifetime lifetime, ISolution solution, ChangeManager changeManager, DocumentManager documentManager)
    {
      _solution = solution;
      _documentManager = documentManager;
      //changeManager.Changed2.Advise(lifetime, OnChangeManagerChanged);

      changeManager.RegisterChangeProvider(lifetime, this);
      changeManager.AddDependency(lifetime, this, documentManager.ChangeProvider);
      changeManager.AddDependency(lifetime, this, solution);

      foreach (var project in solution.GetAllProjects())
      {
        Debug.WriteLine(project.Name);
        //var projectItem = project as JetBrains.Proj
        foreach (var file in project.GetAllProjectFiles())
        {
          var ext = System.IO.Path.GetExtension(file.Name);
          if (string.Equals(ext, ".dll", StringComparison.InvariantCultureIgnoreCase))
            continue;

          if (file.LanguageType.Name == "MSBuild")
            continue;

          if (string.Equals(ext, ".dsl", StringComparison.InvariantCultureIgnoreCase))
          {
            var stream = file.CreateReadStream();
            string content = "";
            using (var streamReader = new StreamReader(stream))
              content = streamReader.ReadToEnd();
            Debug.WriteLine(content);
          }

          Debug.WriteLine(file.Name);
        }
        
      }
    }

    private void OnChangeManagerChanged(ChangeEventArgs args)
    {
      //ProjectModelChange
      var change = args.ChangeMap.GetChange<ProjectFileDocumentChange>(_solution);
      if (change != null)
      {
        
      }
      //change.
      //if (change != null)
      //  change.Accept(new ProjectModelChangeVisitor(this));
    }

    private class ProjectModelChangeVisitor : RecursiveProjectModelChangeDeltaVisitor
    {
      private readonly NitraSolutionComponent myOwner;

      public ProjectModelChangeVisitor([NotNull] NitraSolutionComponent owner)
      {
        myOwner = owner;
      }

      public override void VisitItemDelta([NotNull] ProjectItemChange change)
      {
        base.VisitItemDelta(change);

        var file = change.ProjectItem as IProjectFile;
        if (file != null && string.Equals(file.Location.ExtensionWithDot, ".dsl", StringComparison.InvariantCultureIgnoreCase))
        {
        }
        else
        {
        }
      }


      public override void VisitProjectReferenceDelta([NotNull] ProjectReferenceChange change)
      {
        base.VisitProjectReferenceDelta(change);

        var project = change.GetOldProject();
        if (project != null && project.IsValid())
        {
        }
      }
    }

    #region IChangeProvider Members

    public object Execute(IChangeMap changeMap)
    {
      var documentChange = changeMap.GetChange<ProjectFileDocumentChange>(_documentManager.ChangeProvider);
      if (documentChange != null)
      {
      }

      var projectModelChange = changeMap.GetChange<ProjectModelChange>(_solution);
      //if (projectModelChange != null)
      //  OnProjectModelChanged(projectModelChange);
      return null;
    }

    #endregion
  }
}
