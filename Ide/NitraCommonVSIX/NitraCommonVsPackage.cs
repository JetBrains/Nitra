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

    public static NitraCommonVsPackage Instance;

    private RunningDocTableEvents                       _runningDocTableEventse;
    private Dictionary<IVsHierarchy, HierarchyListener> _listenersMap = new Dictionary<IVsHierarchy, HierarchyListener>();
    private string                                      _loadingProjectPath;
    private List<Server>                                _servers = new List<Server>();
    private StringManager                               _stringManager = new StringManager();
    private string                                      _currentSolutionPath;
    private SolutionId                                  _currentSolutionId;
    private ProjectId                                   _loadingProjectId;
    private uint                                        _objectManagerCookie;
    private Library                                     _library;

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

    private void SolutionEvents_OnQueryUnloadProject(object sender, CancelHierarchyEventArgs e)
    {
      var hierarchy = e.Hierarchy;
      var project = hierarchy.GetProp<EnvDTE.Project>(VSConstants.VSITEMID_ROOT, __VSHPROPID.VSHPROPID_ExtObject);
      Debug.WriteLine($"tr: QueryUnloadProject(FullName='{project.FullName}')");
    }

    private void SolutionEvents_OnQueryCloseSolution(object sender, CancelEventArgs e)
    {
      Debug.WriteLine($"tr: QueryCloseSolution(Cancel='{e.Cancel}')");
    }

    private void SolutionEvents_OnQueryCloseProject(object sender, QueryCloseProjectEventArgs e)
    {
      var hierarchy = e.Hierarchy;
      var project = hierarchy.GetProp<EnvDTE.Project>(VSConstants.VSITEMID_ROOT, __VSHPROPID.VSHPROPID_ExtObject);
      Debug.WriteLine($"tr: QueryCloseProject(IsRemoving='{e.IsRemoving}', Cancel='{e.Cancel}', FullName='{project?.FullName}')");
    }

    private void SolutionEvents_OnQueryChangeProjectParent(object sender, QueryChangeProjectParentEventArgs e)
    {
      Debug.WriteLine($"tr: QueryChangeProjectParent(Hierarchy='{e.Hierarchy}', NewParentHierarchy='{e.NewParentHierarchy}', Cancel='{e.Cancel}')");
    }

    private void SolutionEvents_OnQueryBackgroundLoadProjectBatch(object sender, QueryLoadProjectBatchEventArgs e)
    {
      Debug.WriteLine($"tr: QueryBackgroundLoadProjectBatch(ShouldDelayLoadToNextIdle='{e.ShouldDelayLoadToNextIdle}')");
    }

    private void SolutionEvents_OnBeforeUnloadProject(object sender, LoadProjectEventArgs e)
    {
      Debug.WriteLine($"tr: BeforeUnloadProject(RealHierarchy='{e.RealHierarchy}', StubHierarchy='{e.StubHierarchy}')");
    }

    private void SolutionEvents_OnBeforeOpenSolution(object sender, BeforeOpenSolutionEventArgs e)
    {
      var stringManager = _stringManager;
      if (NitraCommonPackage.Configs.Count == 0)
      {
      }

      foreach (var config in NitraCommonPackage.Configs)
      {
        var server = new Server(stringManager, config, this);
        _servers.Add(server);
      }

      var solutionPath = e.SolutionFilename;
      var id           = new SolutionId(stringManager.GetId(solutionPath));

      _currentSolutionPath = solutionPath;
      _currentSolutionId = id;

      foreach (var server in _servers)
        server.SolutionStartLoading(id, solutionPath);

      Debug.WriteLine($"tr: BeforeOpenSolution(SolutionFilename='{solutionPath}' id={id})");
    }

    private void SolutionEvents_OnBeforeOpenProject(object sender, BeforeOpenProjectEventArgs e)
    {
      Debug.WriteLine($"tr: BeforeOpenProject(Filename='{e.Filename}', Project='{e.Project}'  ProjectType='{e.ProjectType}')");
    }

    private void SolutionEvents_OnBeforeOpeningChildren(object sender, HierarchyEventArgs e)
    {
      Debug.WriteLine($"tr: BeforeOpeningChildren(Hierarchy='{e.Hierarchy}')");
    }

    private void SolutionEvents_OnBeforeLoadProjectBatch(object sender, LoadProjectBatchEventArgs e)
    {
      Debug.WriteLine($"tr: BeforeLoadProjectBatch(IsBackgroundIdleBatch='{e.IsBackgroundIdleBatch}')");
    }

    private void SolutionEvents_OnBeforeClosingChildren(object sender, HierarchyEventArgs e)
    {
      Debug.WriteLine($"tr: BeforeClosingChildren(Hierarchy='{e.Hierarchy}')");
    }

    private void SolutionEvents_OnBeforeCloseSolution(object sender, EventArgs e)
    {
      foreach (var server in _servers)
        server.Dispose();

      _servers.Clear();

      Debug.WriteLine($"tr: BeforeCloseSolution()");
    }

    private void SolutionEvents_OnBeforeBackgroundSolutionLoadBegins(object sender, EventArgs e)
    {
      Debug.WriteLine($"tr: BeforeBackgroundSolutionLoadBegins()");
    }

    private void SolutionEvents_OnAfterRenameProject(object sender, HierarchyEventArgs e)
    {
      var hierarchy = e.Hierarchy;
      var project = hierarchy.GetProp<EnvDTE.Project>(VSConstants.VSITEMID_ROOT, __VSHPROPID.VSHPROPID_ExtObject);
      Debug.WriteLine($"tr: AfterRenameProject(Hierarchy='{hierarchy}', FullName='{project.FullName}')");
    }

    private void SolutionEvents_OnAfterOpenSolution(object sender, OpenSolutionEventArgs e)
    {
      foreach (var server in _servers)
        server.SolutionLoaded(_currentSolutionId);

      Debug.WriteLine($"tr: AfterOpenSolution(IsNewSolution='{e.IsNewSolution}', Id='{_currentSolutionId}')");
    }

    private void SolutionEvents_OnAfterOpenProject(object sender, OpenProjectEventArgs e)
    {
      var hierarchy = e.Hierarchy;
      var project = hierarchy.GetProp<EnvDTE.Project>(VSConstants.VSITEMID_ROOT, __VSHPROPID.VSHPROPID_ExtObject);

      if (project == null)
        return; // not supported prfoject type

      var projectId   = new ProjectId(_stringManager.GetId(project.FullName));
      var projectPath = project.FullName;

      _loadingProjectPath = projectPath;
      _loadingProjectId   = projectId;

      foreach (var server in _servers)
        server.ProjectStartLoading(projectId, projectPath);

      Debug.WriteLine($"tr: AfterOpenProject(IsAdded='{e.IsAdded}', FullName='{projectPath}' id={projectId})");

      foreach (var server in _servers)
        server.AddedMscorlibReference(projectId);

      var listener = new HierarchyListener(hierarchy);

      listener.ItemAdded      += FileAdded;
      listener.ItemDeleted    += FileDeleted;
      listener.ReferenceAdded += Listener_ReferenceAdded;
      listener.StartListening(true);

      _listenersMap.Add(hierarchy, listener);

      // We need apdate all references when a project adding in exist solution
      if (e.IsAdded)
      {
      }

      foreach (var server in _servers)
        server.ProjectLoaded(projectId);
    }

    private void Listener_ReferenceAdded(object sender, ReferenceEventArgs e)
    {
      var r             = e.Reference;
      var sourceProject = r.SourceProject; // TODO: Add support of project reference
      var path          = r.Path;
      var projectPath   = r.ContainingProject.FileName;
      var projectId     = new ProjectId(_stringManager.GetId(projectPath));

      foreach (var server in _servers)
        server.ReferenceAdded(projectId, path);

      Debug.WriteLine($"tr: ReferenceAdded(FileName='{e.Reference.Path}' projectId={projectId})");
    }

    private void SolutionEvents_OnBeforeCloseProject(object sender, CloseProjectEventArgs e)
    {
      var hierarchy = e.Hierarchy;

      var listener = _listenersMap[hierarchy];
      listener.StopListening();
      listener.Dispose();
      _listenersMap.Remove(hierarchy);

      var project   = hierarchy.GetProp<EnvDTE.Project>(VSConstants.VSITEMID_ROOT, __VSHPROPID.VSHPROPID_ExtObject);

      if (project == null)
        return;

      var path      = project.FullName;
      var id        = new ProjectId(_stringManager.GetId(path));

      Debug.WriteLine($"tr: BeforeCloseProject(IsRemoved='{e.IsRemoved}', FullName='{project.FullName}' id={id})");

      foreach (var server in _servers)
        server.BeforeCloseProject(id);
    }

    private void FileAdded(object sender, HierarchyItemEventArgs e)
    {
      var path      = e.FileName;
      var id        = new FileId(_stringManager.GetId(path));

      string action = e.Hierarchy.GetProp<string>(e.ItemId, __VSHPROPID4.VSHPROPID_BuildAction);

      if (action == "Compile" || action == "Nitra")
      {
        object obj;
        var hr2 = e.Hierarchy.GetProperty(e.ItemId, (int)__VSHPROPID.VSHPROPID_ExtObject, out obj);

        var projectItem = obj as EnvDTE.ProjectItem;
        if (ErrorHelper.Succeeded(hr2) && projectItem != null)
        {
          var projectPath = projectItem.ContainingProject.FileName;
          var projectId = new ProjectId(_stringManager.GetId(projectPath));

          foreach (var server in _servers)
            server.FileAdded(projectId, path, id, new FileVersion());

          Debug.WriteLine($"tr: FileAdded(BuildAction='{action}', FileName='{path}' projectId={projectId})");
          return;
        }
      }

      Debug.WriteLine($"tr: FileAdded(BuildAction='{action}', FileName='{path}')");
    }

    private void FileDeleted(object sender, HierarchyItemEventArgs e)
    {
      var path      = e.FileName;
      var id        = new FileId(_stringManager.GetId(path));

      string action = e.Hierarchy.GetProp<string>(e.ItemId, __VSHPROPID4.VSHPROPID_BuildAction);

      if (action == "Compile" || action == "Nitra")
        foreach (var server in _servers)
          server.FileUnloaded(id);

      Debug.WriteLine($"tr: FileAdded(FileName='{path}' id={id})");
    }

    private void SolutionEvents_OnAfterOpeningChildren(object sender, Microsoft.VisualStudio.Shell.Events.HierarchyEventArgs e)
    {
      Debug.WriteLine($"tr: AfterOpeningChildren(Hierarchy='{e.Hierarchy}')");
    }

    private void SolutionEvents_OnAfterMergeSolution(object sender, EventArgs e)
    {
      Debug.WriteLine($"tr: AfterMergeSolution()");
    }

    private void SolutionEvents_OnAfterLoadProjectBatch(object sender, LoadProjectBatchEventArgs e)
    {
      Debug.WriteLine($"tr: AfterLoadProjectBatch(IsBackgroundIdleBatch='{e.IsBackgroundIdleBatch}' _loadingProjectId={_loadingProjectId})");
    }

    private void SolutionEvents_OnAfterLoadProject(object sender, LoadProjectEventArgs e)
    {
      Debug.WriteLine($"tr: AfterLoadProject(RealHierarchy='{e.RealHierarchy}', StubHierarchy='{e.StubHierarchy}')");
    }

    private void SolutionEvents_OnAfterClosingChildren(object sender, Microsoft.VisualStudio.Shell.Events.HierarchyEventArgs e)
    {
      Debug.WriteLine($"tr: AfterClosingChildren(Hierarchy='{e.Hierarchy}')");
    }

    private void SolutionEvents_OnAfterCloseSolution(object sender, EventArgs e)
    {
      Debug.WriteLine("tr: AfterCloseSolution()");
    }

    private void SolutionEvents_OnAfterChangeProjectParent(object sender, Microsoft.VisualStudio.Shell.Events.HierarchyEventArgs e)
    {
      Debug.WriteLine($"tr: AfterChangeProjectParent(Hierarchy='{e.Hierarchy}')");
    }

    private void SolutionEvents_OnAfterBackgroundSolutionLoadComplete(object sender, EventArgs e)
    {
      foreach (var server in _servers)
        server.SolutionLoaded(_currentSolutionId);

      Debug.WriteLine($"tr: AfterBackgroundSolutionLoadComplete(_currentSolutionId={_currentSolutionId})");
    }

    private void SolutionEvents_OnAfterAsynchOpenProject(object sender, OpenProjectEventArgs e)
    {
      Debug.WriteLine($"tr: AfterChangeProjectParent(Hierarchy='{e.Hierarchy}', IsAdded='{e.IsAdded}' _currentSolutionId={_currentSolutionId})");
    }

    private void OnDocumentWindowOnScreenChanged(object sender, DocumentWindowOnScreenChangedEventArgs e)
    {
      var fullPath    = e.Info.FullPath;
      var id          = new FileId(_stringManager.GetId(fullPath));
      var windowFrame = e.Info.WindowFrame;
      var vsTextView  = VsShellUtilities.GetTextView(windowFrame);
      var wpfTextView = vsTextView.ToIWpfTextView();
      if (wpfTextView == null)
        return;
      var dispatcher  = wpfTextView.VisualElement.Dispatcher;
      var hierarchy   = windowFrame.GetHierarchyFromVsWindowFrame();

      if (e.OnScreen)
        foreach (var server in _servers)
          server.ViewActivated(wpfTextView, id, hierarchy, fullPath);
      else
        foreach (var server in _servers)
          server.ViewDeactivated(wpfTextView, id);
    }

    private void OnDocumentWindowDestroy(object sender, DocumentWindowEventArgs e)
    {
      var windowFrame = e.Info.WindowFrame;
      var vsTextView = VsShellUtilities.GetTextView(windowFrame);
      var wpfTextView = vsTextView.ToIWpfTextView();
      if (wpfTextView == null)
        return;
      foreach (var server in _servers)
        server.DocumentWindowDestroy(wpfTextView);
    }

    private void SubscibeToSolutionEvents()
    {
      SolutionEvents.OnAfterAsynchOpenProject += SolutionEvents_OnAfterAsynchOpenProject;
      SolutionEvents.OnAfterBackgroundSolutionLoadComplete += SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
      SolutionEvents.OnAfterChangeProjectParent += SolutionEvents_OnAfterChangeProjectParent;
      SolutionEvents.OnAfterCloseSolution += SolutionEvents_OnAfterCloseSolution;
      SolutionEvents.OnAfterClosingChildren += SolutionEvents_OnAfterClosingChildren;
      SolutionEvents.OnAfterLoadProject += SolutionEvents_OnAfterLoadProject;
      SolutionEvents.OnAfterLoadProjectBatch += SolutionEvents_OnAfterLoadProjectBatch;
      SolutionEvents.OnAfterMergeSolution += SolutionEvents_OnAfterMergeSolution;
      SolutionEvents.OnAfterOpeningChildren += SolutionEvents_OnAfterOpeningChildren;
      SolutionEvents.OnAfterOpenProject += SolutionEvents_OnAfterOpenProject;
      SolutionEvents.OnAfterOpenSolution += SolutionEvents_OnAfterOpenSolution;
      SolutionEvents.OnAfterRenameProject += SolutionEvents_OnAfterRenameProject;
      SolutionEvents.OnBeforeBackgroundSolutionLoadBegins += SolutionEvents_OnBeforeBackgroundSolutionLoadBegins;
      SolutionEvents.OnBeforeCloseProject += SolutionEvents_OnBeforeCloseProject;
      SolutionEvents.OnBeforeCloseSolution += SolutionEvents_OnBeforeCloseSolution;
      SolutionEvents.OnBeforeClosingChildren += SolutionEvents_OnBeforeClosingChildren;
      SolutionEvents.OnBeforeLoadProjectBatch += SolutionEvents_OnBeforeLoadProjectBatch;
      SolutionEvents.OnBeforeOpeningChildren += SolutionEvents_OnBeforeOpeningChildren;
      SolutionEvents.OnBeforeOpenProject += SolutionEvents_OnBeforeOpenProject;
      SolutionEvents.OnBeforeOpenSolution += SolutionEvents_OnBeforeOpenSolution;
      SolutionEvents.OnBeforeUnloadProject += SolutionEvents_OnBeforeUnloadProject;
      SolutionEvents.OnQueryBackgroundLoadProjectBatch += SolutionEvents_OnQueryBackgroundLoadProjectBatch;
      SolutionEvents.OnQueryChangeProjectParent += SolutionEvents_OnQueryChangeProjectParent;
      SolutionEvents.OnQueryCloseProject += SolutionEvents_OnQueryCloseProject;
      SolutionEvents.OnQueryCloseSolution += SolutionEvents_OnQueryCloseSolution;
      SolutionEvents.OnQueryUnloadProject += SolutionEvents_OnQueryUnloadProject;

      _runningDocTableEventse.DocumentWindowOnScreenChanged += OnDocumentWindowOnScreenChanged;
      _runningDocTableEventse.DocumentWindowDestroy         += OnDocumentWindowDestroy;
    }

    private void UnsubscibeToSolutionEvents()
    {
      SolutionEvents.OnAfterAsynchOpenProject -= SolutionEvents_OnAfterAsynchOpenProject;
      SolutionEvents.OnAfterBackgroundSolutionLoadComplete -= SolutionEvents_OnAfterBackgroundSolutionLoadComplete;
      SolutionEvents.OnAfterChangeProjectParent -= SolutionEvents_OnAfterChangeProjectParent;
      SolutionEvents.OnAfterCloseSolution -= SolutionEvents_OnAfterCloseSolution;
      SolutionEvents.OnAfterClosingChildren -= SolutionEvents_OnAfterClosingChildren;
      SolutionEvents.OnAfterLoadProject -= SolutionEvents_OnAfterLoadProject;
      SolutionEvents.OnAfterLoadProjectBatch -= SolutionEvents_OnAfterLoadProjectBatch;
      SolutionEvents.OnAfterMergeSolution -= SolutionEvents_OnAfterMergeSolution;
      SolutionEvents.OnAfterOpeningChildren -= SolutionEvents_OnAfterOpeningChildren;
      SolutionEvents.OnAfterOpenProject -= SolutionEvents_OnAfterOpenProject;
      SolutionEvents.OnAfterOpenSolution -= SolutionEvents_OnAfterOpenSolution;
      SolutionEvents.OnAfterRenameProject -= SolutionEvents_OnAfterRenameProject;
      SolutionEvents.OnBeforeBackgroundSolutionLoadBegins -= SolutionEvents_OnBeforeBackgroundSolutionLoadBegins;
      SolutionEvents.OnBeforeCloseProject -= SolutionEvents_OnBeforeCloseProject;
      SolutionEvents.OnBeforeCloseSolution -= SolutionEvents_OnBeforeCloseSolution;
      SolutionEvents.OnBeforeClosingChildren -= SolutionEvents_OnBeforeClosingChildren;
      SolutionEvents.OnBeforeLoadProjectBatch -= SolutionEvents_OnBeforeLoadProjectBatch;
      SolutionEvents.OnBeforeOpeningChildren -= SolutionEvents_OnBeforeOpeningChildren;
      SolutionEvents.OnBeforeOpenProject -= SolutionEvents_OnBeforeOpenProject;
      SolutionEvents.OnBeforeOpenSolution -= SolutionEvents_OnBeforeOpenSolution;
      SolutionEvents.OnBeforeUnloadProject -= SolutionEvents_OnBeforeUnloadProject;
      SolutionEvents.OnQueryBackgroundLoadProjectBatch -= SolutionEvents_OnQueryBackgroundLoadProjectBatch;
      SolutionEvents.OnQueryChangeProjectParent -= SolutionEvents_OnQueryChangeProjectParent;
      SolutionEvents.OnQueryCloseProject -= SolutionEvents_OnQueryCloseProject;
      SolutionEvents.OnQueryCloseSolution -= SolutionEvents_OnQueryCloseSolution;
      SolutionEvents.OnQueryUnloadProject -= SolutionEvents_OnQueryUnloadProject;
    }
  }
}
