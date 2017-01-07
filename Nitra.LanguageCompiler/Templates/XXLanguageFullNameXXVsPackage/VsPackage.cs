#pragma warning disable VSSDK002 // Visual Studio service should be used on main thread explicitly.

namespace XXNamespaceXX
{
  using Microsoft.VisualStudio;
  using Microsoft.VisualStudio.Shell;
  using Microsoft.VisualStudio.Shell.Events;
  using Microsoft.VisualStudio.Shell.Interop;

  using NitraCommonIde;

  using System;
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.ComponentModel;
  using System.Diagnostics;
  using System.IO;
  using System.Reflection;
  using System.Runtime.InteropServices;

  /// <summary>
  /// This class implements the package exposed by this assembly.
  /// </summary>
  /// <remarks>
  /// This package is required if you want to define adds custom commands (ctmenu)
  /// or localized resources for the strings that appear in the New Project and Open Project dialogs.
  /// Creating project extensions or project types does not actually require a VSPackage.
  /// </remarks>
  [PackageRegistration(UseManagedResourcesOnly = true)]
  [Description("Nitra Package for XXLanguageFullNameXX language.")]
  [ProvideAutoLoad(UIContextGuids80.NoSolution)]
  [Guid(XXLanguageXXGuids.PackageGuid)]
  // This attribute is used to register the information needed to show this package in the Help/About dialog of Visual Studio.
  [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
  [ProvideBindingPath(SubPath= "Languages")]
  public sealed class VsPackage : Package
  {
    public static VsPackage Instance;

    static VsPackage()
    {
    }

    public VsPackage()
    {
      Instance = this;
    }

    protected override void Initialize()
    {
      base.Initialize();
      var assembly         = "XXProjectSupportAssemblyXX";

      if (string.IsNullOrEmpty(assembly))
        return;

      var assemblyFullPath = Path.Combine(VsUtils.GetPlaginPath(), @"Languages\XXProjectSupportAssemblyXX");
      var projectSupport   = new ProjectSupport("XXProjectSupportXX", "XXProjectSupportClassXX", Path.Combine(VsUtils.GetPlaginPath(), assemblyFullPath));
      var path             = Path.Combine(VsUtils.GetPlaginPath(), @"Languages\XXProjectSupportAssemblyXX");
      var extensions       = ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase, XXFileExtensionsXX);
      var languages = new []
        {
          new LanguageInfo("XXLanguageFullNameXX", path, extensions)
        };

      var config = new Config(projectSupport, languages);
      NitraCommonPackage.AddProjectType(config);
    }

    protected override void Dispose(bool disposing)
    {
      try
      {
      }
      finally
      {
        base.Dispose(disposing);
      }
    }
  }
}

#pragma warning restore VSSDK002 // Visual Studio service should be used on main thread explicitly.
