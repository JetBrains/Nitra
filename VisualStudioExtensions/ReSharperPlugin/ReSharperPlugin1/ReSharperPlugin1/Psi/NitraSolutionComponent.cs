using System;
using System.IO;
using System.Text;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.DataFlow;
using JetBrains.DocumentManagers;
using JetBrains.DocumentManagers.impl;
using JetBrains.ProjectModel;
using System.Diagnostics;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Caches;

namespace ReSharperPlugin1
{
  [PsiComponent]
  [SolutionComponent]
  class NitraSolutionComponent : IChangeProvider, ICache
  {
    private ISolution _solution;
    private DocumentManager _documentManager;
    private IShellLocks _locks;
    private IPsiConfiguration _psiConfiguration;
    private IPersistentIndexManager _persistentIndexManager;

    public NitraSolutionComponent(Lifetime lifetime, ISolution solution, ChangeManager changeManager, DocumentManager documentManager,
      IShellLocks locks, IPsiConfiguration psiConfiguration, IPersistentIndexManager persistentIndexManager)
    {
      _persistentIndexManager = persistentIndexManager;
      _psiConfiguration = psiConfiguration;
      _locks = locks;
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

    #region ICache Members

    public object Build(IPsiSourceFile sourceFile, bool isStartup)
    {
      return false;
    }

    public void Drop(IPsiSourceFile sourceFile)
    {
    }

    public bool HasDirtyFiles
    {
      get { return false; }
    }

    public object Load(JetBrains.Application.Progress.IProgressIndicator progress, bool enablePersistence)
    {
      return true;
    }

    public void MarkAsDirty(IPsiSourceFile sf)
    {
    }

    public void Merge(IPsiSourceFile sourceFile, object builtPart)
    {
    }

    public void MergeLoaded(object data)
    {
    }

    public void OnDocumentChange(IPsiSourceFile sourceFile, ProjectFileDocumentCopyChange change)
    {
    }

    public void OnPsiChange(JetBrains.ReSharper.Psi.Tree.ITreeNode elementContainingChanges, PsiChangedElementType type)
    {
      if (elementContainingChanges != null)
      {
      }
    }

    public void Save(JetBrains.Application.Progress.IProgressIndicator progress, bool enablePersistence)
    {
    }

    public void SyncUpdate(bool underTransaction)
    {
    }

    public bool UpToDate(IPsiSourceFile sourceFile)
    {
      return true;
    }

    #endregion
  }
}
