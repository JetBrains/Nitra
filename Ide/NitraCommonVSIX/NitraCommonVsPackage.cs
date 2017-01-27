//------------------------------------------------------------------------------
// <copyright file="VSPackage.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;

using Nitra.ClientServer.Client;
using Nitra.ClientServer.Messages;
using Nitra.VisualStudio;
using NitraCommonIde;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Nitra.VisualStudio
{
  /// <summary>
  /// This is the class that implements the package exposed by this assembly.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The minimum requirement for a class to be considered a valid package for Visual Studio
  /// is to implement the IVsPackage interface and register itself with the shell.
  /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
  /// to do it: it derives from the Package class that provides the implementation of the
  /// IVsPackage interface and uses the registration attributes defined in the framework to
  /// register itself and its components with the shell. These attributes tell the pkgdef creation
  /// utility what data to put into .pkgdef file.
  /// </para>
  /// <para>
  /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
  /// </para>
  /// </remarks>
  [ProvideAutoLoad(UIContextGuids80.NoSolution)]
  [Description("Nitra Package.")]
  [PackageRegistration(UseManagedResourcesOnly = true)]
  [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
  [Guid(NitraCommonVsPackage.PackageGuidString)]
  [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
  public sealed class NitraCommonVsPackage : Package
  {
    /// <summary>VSPackage GUID string.</summary>
    public const string PackageGuidString = "66c3f4cd-1547-458b-a321-83f0c448b4d3";
    public static SolutionId InvalidSolutionId = new SolutionId(-1);


    public static NitraCommonVsPackage Instance;

    RunningDocTableEvents                       _runningDocTableEventse;
    Dictionary<IVsHierarchy, HierarchyListener> _listenersMap = new Dictionary<IVsHierarchy, HierarchyListener>();
    List<EnvDTE.Project>                        _projects = new List<EnvDTE.Project>();
    List<Server>                                _servers = new List<Server>();
    StringManager                               _stringManager = new StringManager();
    uint                                        _objectManagerCookie;
    Library                                     _library;
    SolutionLoadingSate                         _backgroundLoading;
    SolutionId                                  _currentSolutionId = InvalidSolutionId;

    /// <summary>
    /// Initializes a new instance of the <see cref="NitraCommonVsPackage"/> class.
    /// </summary>
    public NitraCommonVsPackage()
    {
      // Inside this method you can place any initialization code that does not require
      // any Visual Studio service because at this point the package object is created but
      // not sited yet inside Visual Studio environment. The place to do all the other
      // initialization is the Initialize method.
    }

    public void SetFindResult(IVsSimpleObjectList2 findResults)
    {
      _library.OnFindAllReferencesDone(findResults);
    }

    #region Package Members

    /// <summary>
    /// Initialization of the package; this method is called right after the package is sited, so this is the place
    /// where you can put all the initialization code that rely on services provided by VisualStudio.
    /// </summary>
    protected override void Initialize()
    {
      base.Initialize();
      Debug.Assert(Instance == null);
      Instance = this;

      _runningDocTableEventse = new RunningDocTableEvents();
      SubscibeToSolutionEvents();


      if (_objectManagerCookie == 0)
      {
        _library = new Library();
        var objManager = this.GetService(typeof(SVsObjectManager)) as IVsObjectManager2;

        if (null != objManager)
          ErrorHandler.ThrowOnFailure(objManager.RegisterSimpleLibrary(_library, out _objectManagerCookie));
      }
    }

    protected override void Dispose(bool disposing)
    {
      try
      {
        foreach (var server in _servers)
          server.Dispose();

        UnsubscibeToSolutionEvents();
        _runningDocTableEventse?.Dispose();
        _runningDocTableEventse = null;

        var objManager = GetService(typeof(SVsObjectManager)) as IVsObjectManager2;
        if (objManager != null)
          objManager.UnregisterLibrary(_objectManagerCookie);
      }
      finally
      {
        base.Dispose(disposing);
      }
    }

    #endregion

    // /////////////////////////////////////////////////////////////////////////////////////////////

    void QueryUnloadProject(object sender, CancelHierarchyEventArgs e)
    {
      var hierarchy = e.Hierarchy;
      var project = hierarchy.GetProp<EnvDTE.Project>(VSConstants.VSITEMID_ROOT, __VSHPROPID.VSHPROPID_ExtObject);
      Debug.WriteLine($"tr: QueryUnloadProject(FullName='{project.FullName}')");
    }

    void SolutionEvents_OnQueryCloseSolution(object sender, CancelEventArgs e)
    {
      Debug.WriteLine($"tr: QueryCloseSolution(Cancel='{e.Cancel}')");
    }

    void QueryCloseProject(object sender, QueryCloseProjectEventArgs e)
    {
      var hierarchy = e.Hierarchy;
      var project = hierarchy.GetProp<EnvDTE.Project>(VSConstants.VSITEMID_ROOT, __VSHPROPID.VSHPROPID_ExtObject);
      Debug.WriteLine($"tr: QueryCloseProject(IsRemoving='{e.IsRemoving}', Cancel='{e.Cancel}', FullName='{project?.FullName}')");
    }

    void QueryChangeProjectParent(object sender, QueryChangeProjectParentEventArgs e)
    {
      Debug.WriteLine($"tr: QueryChangeProjectParent(Hierarchy='{e.Hierarchy}', NewParentHierarchy='{e.NewParentHierarchy}', Cancel='{e.Cancel}')");
    }

    void QueryBackgroundLoadProjectBatch(object sender, QueryLoadProjectBatchEventArgs e)
    {
      Debug.WriteLine($"tr: QueryBackgroundLoadProjectBatch(ShouldDelayLoadToNextIdle='{e.ShouldDelayLoadToNextIdle}')");
    }

    void BeforeUnloadProject(object sender, LoadProjectEventArgs e)
    {
      Debug.WriteLine($"tr: BeforeUnloadProject(RealHierarchy='{e.RealHierarchy}', StubHierarchy='{e.StubHierarchy}')");
    }

    void BeforeOpenSolution(object sender, BeforeOpenSolutionEventArgs e)
    {
      _backgroundLoading = SolutionLoadingSate.SynchronousLoading;

      InitServers();

      var solutionPath = e.SolutionFilename;
      var id = new SolutionId(_stringManager.GetId(solutionPath));

      _currentSolutionId = id;

      foreach (var server in _servers)
        server.SolutionStartLoading(id, solutionPath);

      Debug.WriteLine($"tr: BeforeOpenSolution(SolutionFilename='{solutionPath}' id={id})");
    }

    private void InitServers()
    {
      if (_servers.Count > 0)
        return; // allredy initialised

      if (NitraCommonPackage.Configs.Count == 0)
      {
        Debug.WriteLine($"Error: Configs is empty!)");
      }

      var stringManager = _stringManager;

      foreach (var config in NitraCommonPackage.Configs)
      {
        var server = new Server(stringManager, config, this);
        _servers.Add(server);
      }

      return;
    }

    void BeforeOpenProject(object sender, BeforeOpenProjectEventArgs e)
    {
      Debug.WriteLine($"tr: BeforeOpenProject(Filename='{e.Filename}', Project='{e.Project}'  ProjectType='{e.ProjectType}')");
    }

    void BeforeOpeningChildren(object sender, HierarchyEventArgs e)
    {
      Debug.WriteLine($"tr: BeforeOpeningChildren(Hierarchy='{e.Hierarchy}')");
    }

    void BeforeLoadProjectBatch(object sender, LoadProjectBatchEventArgs e)
    {
      Debug.WriteLine($"tr: BeforeLoadProjectBatch(IsBackgroundIdleBatch='{e.IsBackgroundIdleBatch}')");
    }

    void BeforeClosingChildren(object sender, HierarchyEventArgs e)
    {
      Debug.WriteLine($"tr: BeforeClosingChildren(Hierarchy='{e.Hierarchy}')");
    }

    void BeforeCloseSolution(object sender, EventArgs e)
    {
      foreach (var server in _servers)
        server.Dispose();

      _servers.Clear();

      Debug.WriteLine($"tr: BeforeCloseSolution()");
    }

    void BeforeBackgroundSolutionLoadBegins(object sender, EventArgs e)
    {
      _backgroundLoading = SolutionLoadingSate.AsynchronousLoading;

      Debug.WriteLine($"tr: BeforeBackgroundSolutionLoadBegins()");
    }

    void AfterRenameProject(object sender, HierarchyEventArgs e)
    {
      var hierarchy = e.Hierarchy;
      var project = hierarchy.GetProp<EnvDTE.Project>(VSConstants.VSITEMID_ROOT, __VSHPROPID.VSHPROPID_ExtObject);
      Debug.WriteLine($"tr: AfterRenameProject(Hierarchy='{hierarchy}', FullName='{project.FullName}')");
    }

    void AfterOpenSolution(object sender, OpenSolutionEventArgs e)
    {
      var isTemporarySolution = _currentSolutionId == InvalidSolutionId;
      if (isTemporarySolution)
        _currentSolutionId = new SolutionId(0); // This is temporary solution for <MiscFiles>

      InitServers(); // need in case of open separate files (with no project)

      Debug.Assert(_backgroundLoading != SolutionLoadingSate.AsynchronousLoading);

      var path = _stringManager.GetPath(_currentSolutionId);
      Debug.WriteLine($"tr: AfterOpenSolution(IsNewSolution='{e.IsNewSolution}', Id='{_currentSolutionId}' Path='{path}')");

      foreach (var server in _servers)
        if (isTemporarySolution)
          server.SolutionStartLoading(_currentSolutionId, ""); // init "<MiscFiles>" solution

      foreach (var listener in _listenersMap.Values)
        listener.StartListening(true);

      // scan only currently loaded projects
      foreach (var project in _projects)
        ScanReferences(project);

      foreach (var project in _projects)
        foreach (var server in _servers)
          server.ProjectLoaded(GetProjectId(project));

      _projects.Clear();

      _backgroundLoading = SolutionLoadingSate.Loaded;
    }

    void ScanReferences(EnvDTE.Project project)
    {
      Debug.WriteLine("tr: ScanReferences(started)");
      Debug.WriteLine($"tr:  Project: Project='{project.Name}'");

      var vsproject = project.Object as VSLangProj.VSProject;
      if (vsproject != null)
      {
        var projectId = GetProjectId(project);

        foreach (VSLangProj.Reference reference in vsproject.References)
        {
          var path = reference.Path;

          if (reference.SourceProject == null)
          {
            if (path == null)
              Debug.WriteLine($"tr:    Error: reference.Path=null reference.Name={reference.Name}");

            foreach (var server in _servers)
              server.ReferenceAdded(projectId, path);
            Debug.WriteLine($"tr:    Reference: Name={reference.Name} Path={path}");
          }
          else
          {
            var referencedProjectId = GetProjectId(reference.SourceProject);
            foreach (var server in _servers)
              server.ProjectReferenceAdded(projectId, referencedProjectId, path);
            Debug.WriteLine($"tr:    Project reference: ProjectId={referencedProjectId} Project={reference.SourceProject.Name} ProjectPath={reference.SourceProject.FullName} DllPath={reference.Path}");
          }
        }
      }
      else
        Debug.WriteLine("tr:    Error: project.Object=null");

      Debug.WriteLine("tr: ScanReferences(finished)");
    }

    ProjectId GetProjectId(EnvDTE.Project project)
    {
      return new ProjectId(_stringManager.GetId(project.FullName));
    }

    void AfterBackgroundSolutionLoadComplete(object sender, EventArgs e)
    {
      var path = _stringManager.GetPath(_currentSolutionId);
      Debug.WriteLine($"tr: AfterBackgroundSolutionLoadComplete(Id={_currentSolutionId} Path='{path}')");

      foreach (var server in _servers)
        server.SolutionLoaded(_currentSolutionId);

      _backgroundLoading = SolutionLoadingSate.Loaded;
    }

    void AfterOpenProject(object sender, OpenProjectEventArgs e)
    {
      var hierarchy = e.Hierarchy;
      var project = hierarchy.GetProp<EnvDTE.Project>(VSConstants.VSITEMID_ROOT, __VSHPROPID.VSHPROPID_ExtObject);

      if (project == null)
        return; // not supported prfoject type

      var projectPath = project.FullName;
      var projectId = new ProjectId(_stringManager.GetId(projectPath));

      var isMiscFiles = string.IsNullOrEmpty(projectPath);
      var isDelayLoading = _backgroundLoading != SolutionLoadingSate.AsynchronousLoading && !isMiscFiles;

      if (isDelayLoading)
        _projects.Add(project);

      foreach (var server in _servers)
        server.ProjectStartLoading(projectId, projectPath);

      Debug.WriteLine($"tr: AfterOpenProject(IsAdded='{e.IsAdded}', FullName='{projectPath}' id={projectId} Name={project.Name} State={_backgroundLoading})");

      foreach (var server in _servers)
        server.AddedMscorlibReference(projectId);

      var listener = new HierarchyListener(hierarchy);

      listener.ItemAdded      += FileAdded;
      listener.ItemDeleted    += FileDeleted;
      listener.ReferenceAdded += ReferenceAdded;

      _listenersMap.Add(hierarchy, listener);

      // We need apdate all references when a project adding in exist solution
      if (e.IsAdded)
      {
      }

      if (!isDelayLoading)
      {
        listener.StartListening(true);

        foreach (var server in _servers)
          server.ProjectLoaded(GetProjectId(project));
      }
    }

    void ReferenceAdded(object sender, ReferenceEventArgs e)
    {
      if (_backgroundLoading != SolutionLoadingSate.AsynchronousLoading)
        return;

      var r = e.Reference;
      var sourceProject = r.SourceProject; // TODO: Add support of project reference
      var path = r.Path;

      var projectPath = r.ContainingProject.FullName;
      var projectId = new ProjectId(_stringManager.GetId(projectPath));

      if (string.IsNullOrEmpty(path))
      {
        Debug.WriteLine($"tr: Error: ReferenceAdded(FileName='null' Name={r.Name} projectId={projectId})");
        return;
      }

      foreach (var server in _servers)
        server.ReferenceAdded(projectId, path);

      Debug.WriteLine($"tr: ReferenceAdded(FileName='{e.Reference.Path}' projectId={projectId})");
    }

    void SolutionEvents_OnBeforeCloseProject(object sender, CloseProjectEventArgs e)
    {
      var hierarchy = e.Hierarchy;

      if (_listenersMap.ContainsKey(hierarchy))
      {
        var listener = _listenersMap[hierarchy];
        listener.StopListening();
        listener.Dispose();
        _listenersMap.Remove(hierarchy);
      }

      var project   = hierarchy.GetProp<EnvDTE.Project>(VSConstants.VSITEMID_ROOT, __VSHPROPID.VSHPROPID_ExtObject);

      if (project == null)
        return;

      Debug.Assert(_projects.Count == 0);

      var path      = project.FullName;
      var id        = new ProjectId(_stringManager.GetId(path));

      Debug.WriteLine($"tr: BeforeCloseProject(IsRemoved='{e.IsRemoved}', FullName='{project.FullName}' id={id})");

      foreach (var server in _servers)
        server.BeforeCloseProject(id);
    }

    void FileAdded(object sender, HierarchyItemEventArgs e)
    {
      var path = e.FileName;
      var ext  = Path.GetExtension(path);
      var id   = new FileId(_stringManager.GetId(path));

      string action = e.Hierarchy.GetProp<string>(e.ItemId, __VSHPROPID4.VSHPROPID_BuildAction);

      if (action == "Compile" || action == "Nitra" || action == null)
      {
        object obj;
        var hr2 = e.Hierarchy.GetProperty(e.ItemId, (int)__VSHPROPID.VSHPROPID_ExtObject, out obj);

        var projectItem = obj as EnvDTE.ProjectItem;
        if (ErrorHelper.Succeeded(hr2) && projectItem != null)
        {
          var project     = projectItem.ContainingProject;
          var projectPath = project.FullName;
          var projectId   = new ProjectId(_stringManager.GetId(projectPath));

          if (action == null && project.UniqueName != "<MiscFiles>")
          {
            Debug.WriteLine($"tr: FileAdded(BuildAction='{action}', FileName='{path}' projectId={projectId})");
            return;
          }

          foreach (var server in _servers)
            if (server.IsSupportedExtension(ext))
              server.FileAdded(projectId, path, id, new FileVersion());

          Debug.WriteLine($"tr: FileAdded(BuildAction='{action}', FileName='{path}' projectId={projectId})");
          return;
        }
      }

      Debug.WriteLine($"tr: FileAdded(BuildAction='{action}', FileName='{path}')");
    }

    void FileDeleted(object sender, HierarchyItemEventArgs e)
    {
      var path = e.FileName;
      var ext  = Path.GetExtension(path);
      var id   = new FileId(_stringManager.GetId(path));

      string action = e.Hierarchy.GetProp<string>(e.ItemId, __VSHPROPID4.VSHPROPID_BuildAction);
      var project = e.Hierarchy.GetProp<EnvDTE.Project>(VSConstants.VSITEMID_ROOT, __VSHPROPID.VSHPROPID_ExtObject);

      if (action == "Compile" || action == "Nitra" || (action == null && string.IsNullOrEmpty(project.FileName)))
        foreach (var server in _servers)
          if (server.IsSupportedExtension(ext))
            server.FileUnloaded(id);

      Debug.WriteLine($"tr: FileAdded(FileName='{path}' id={id})");
    }

    void AfterOpeningChildren(object sender, Microsoft.VisualStudio.Shell.Events.HierarchyEventArgs e)
    {
      Debug.WriteLine($"tr: AfterOpeningChildren(Hierarchy='{e.Hierarchy}')");
    }

    void SolutionEvents_OnAfterMergeSolution(object sender, EventArgs e)
    {
      Debug.WriteLine($"tr: AfterMergeSolution()");
    }

    void AfterLoadProjectBatch(object sender, LoadProjectBatchEventArgs e)
    {
      Debug.WriteLine($"tr: AfterLoadProjectBatch(IsBackgroundIdleBatch='{e.IsBackgroundIdleBatch}')");
    }

    void AfterLoadProject(object sender, LoadProjectEventArgs e)
    {
      Debug.WriteLine($"tr: AfterLoadProject(RealHierarchy='{e.RealHierarchy}', StubHierarchy='{e.StubHierarchy}')");
    }

    void AfterClosingChildren(object sender, Microsoft.VisualStudio.Shell.Events.HierarchyEventArgs e)
    {
      Debug.WriteLine($"tr: AfterClosingChildren(Hierarchy='{e.Hierarchy}')");
    }

    void AfterCloseSolution(object sender, EventArgs e)
    {
      Debug.Assert(_currentSolutionId != InvalidSolutionId);
      _backgroundLoading = SolutionLoadingSate.NotLoaded;
      var path = _stringManager.GetPath(_currentSolutionId);
      Debug.WriteLine($"tr: AfterCloseSolution(Id={_currentSolutionId} Path='{path}')");
      _currentSolutionId = InvalidSolutionId;
    }

    void AfterChangeProjectParent(object sender, Microsoft.VisualStudio.Shell.Events.HierarchyEventArgs e)
    {
      Debug.WriteLine($"tr: AfterChangeProjectParent(Hierarchy='{e.Hierarchy}')");
    }

    void AfterAsynchOpenProject(object sender, OpenProjectEventArgs e)
    {
      Debug.WriteLine($"tr: AfterChangeProjectParent(Hierarchy='{e.Hierarchy}', IsAdded='{e.IsAdded}' _currentSolutionId={_currentSolutionId})");
    }

    void DocumentWindowOnScreenChanged(object sender, DocumentWindowOnScreenChangedEventArgs e)
    {
      var fullPath    = e.Info.FullPath;
      var ext         = Path.GetExtension(fullPath);
      var id          = new FileId(_stringManager.GetId(fullPath));
      var windowFrame = e.Info.WindowFrame;
      var vsTextView  = VsShellUtilities.GetTextView(windowFrame);
      var wpfTextView = vsTextView.ToIWpfTextView();
      if (wpfTextView == null)
        return;
      var dispatcher  = wpfTextView.VisualElement.Dispatcher;
      var hierarchy   = windowFrame.GetHierarchyFromVsWindowFrame();

      if (e.OnScreen)
      {
        foreach (var server in _servers)
          if (server.IsSupportedExtension(ext))
            server.ViewActivated(wpfTextView, id, hierarchy, fullPath);
      }
      else
      {
        foreach (var server in _servers)
          if (server.IsSupportedExtension(ext))
            server.ViewDeactivated(wpfTextView, id);
      }
    }

    void DocumentWindowDestroy(object sender, DocumentWindowEventArgs e)
    {
      var windowFrame = e.Info.WindowFrame;
      var vsTextView = VsShellUtilities.GetTextView(windowFrame);
      var wpfTextView = vsTextView.ToIWpfTextView();
      if (wpfTextView == null)
        return;
      foreach (var server in _servers)
        server.DocumentWindowDestroy(wpfTextView);
    }

    void SubscibeToSolutionEvents()
    {
      SolutionEvents.OnAfterAsynchOpenProject += AfterAsynchOpenProject;
      SolutionEvents.OnAfterBackgroundSolutionLoadComplete += AfterBackgroundSolutionLoadComplete;
      SolutionEvents.OnAfterChangeProjectParent += AfterChangeProjectParent;
      SolutionEvents.OnAfterCloseSolution += AfterCloseSolution;
      SolutionEvents.OnAfterClosingChildren += AfterClosingChildren;
      SolutionEvents.OnAfterLoadProject += AfterLoadProject;
      SolutionEvents.OnAfterLoadProjectBatch += AfterLoadProjectBatch;
      SolutionEvents.OnAfterMergeSolution += SolutionEvents_OnAfterMergeSolution;
      SolutionEvents.OnAfterOpeningChildren += AfterOpeningChildren;
      SolutionEvents.OnAfterOpenProject += AfterOpenProject;
      SolutionEvents.OnAfterOpenSolution += AfterOpenSolution;
      SolutionEvents.OnAfterRenameProject += AfterRenameProject;
      SolutionEvents.OnBeforeBackgroundSolutionLoadBegins += BeforeBackgroundSolutionLoadBegins;
      SolutionEvents.OnBeforeCloseProject += SolutionEvents_OnBeforeCloseProject;
      SolutionEvents.OnBeforeCloseSolution += BeforeCloseSolution;
      SolutionEvents.OnBeforeClosingChildren += BeforeClosingChildren;
      SolutionEvents.OnBeforeLoadProjectBatch += BeforeLoadProjectBatch;
      SolutionEvents.OnBeforeOpeningChildren += BeforeOpeningChildren;
      SolutionEvents.OnBeforeOpenProject += BeforeOpenProject;
      SolutionEvents.OnBeforeOpenSolution += BeforeOpenSolution;
      SolutionEvents.OnBeforeUnloadProject += BeforeUnloadProject;
      SolutionEvents.OnQueryBackgroundLoadProjectBatch += QueryBackgroundLoadProjectBatch;
      SolutionEvents.OnQueryChangeProjectParent += QueryChangeProjectParent;
      SolutionEvents.OnQueryCloseProject += QueryCloseProject;
      SolutionEvents.OnQueryCloseSolution += SolutionEvents_OnQueryCloseSolution;
      SolutionEvents.OnQueryUnloadProject += QueryUnloadProject;

      _runningDocTableEventse.DocumentWindowOnScreenChanged += DocumentWindowOnScreenChanged;
      _runningDocTableEventse.DocumentWindowDestroy         += DocumentWindowDestroy;
    }

    void UnsubscibeToSolutionEvents()
    {
      SolutionEvents.OnAfterAsynchOpenProject -= AfterAsynchOpenProject;
      SolutionEvents.OnAfterBackgroundSolutionLoadComplete -= AfterBackgroundSolutionLoadComplete;
      SolutionEvents.OnAfterChangeProjectParent -= AfterChangeProjectParent;
      SolutionEvents.OnAfterCloseSolution -= AfterCloseSolution;
      SolutionEvents.OnAfterClosingChildren -= AfterClosingChildren;
      SolutionEvents.OnAfterLoadProject -= AfterLoadProject;
      SolutionEvents.OnAfterLoadProjectBatch -= AfterLoadProjectBatch;
      SolutionEvents.OnAfterMergeSolution -= SolutionEvents_OnAfterMergeSolution;
      SolutionEvents.OnAfterOpeningChildren -= AfterOpeningChildren;
      SolutionEvents.OnAfterOpenProject -= AfterOpenProject;
      SolutionEvents.OnAfterOpenSolution -= AfterOpenSolution;
      SolutionEvents.OnAfterRenameProject -= AfterRenameProject;
      SolutionEvents.OnBeforeBackgroundSolutionLoadBegins -= BeforeBackgroundSolutionLoadBegins;
      SolutionEvents.OnBeforeCloseProject -= SolutionEvents_OnBeforeCloseProject;
      SolutionEvents.OnBeforeCloseSolution -= BeforeCloseSolution;
      SolutionEvents.OnBeforeClosingChildren -= BeforeClosingChildren;
      SolutionEvents.OnBeforeLoadProjectBatch -= BeforeLoadProjectBatch;
      SolutionEvents.OnBeforeOpeningChildren -= BeforeOpeningChildren;
      SolutionEvents.OnBeforeOpenProject -= BeforeOpenProject;
      SolutionEvents.OnBeforeOpenSolution -= BeforeOpenSolution;
      SolutionEvents.OnBeforeUnloadProject -= BeforeUnloadProject;
      SolutionEvents.OnQueryBackgroundLoadProjectBatch -= QueryBackgroundLoadProjectBatch;
      SolutionEvents.OnQueryChangeProjectParent -= QueryChangeProjectParent;
      SolutionEvents.OnQueryCloseProject -= QueryCloseProject;
      SolutionEvents.OnQueryCloseSolution -= SolutionEvents_OnQueryCloseSolution;
      SolutionEvents.OnQueryUnloadProject -= QueryUnloadProject;
    }
  }
}
