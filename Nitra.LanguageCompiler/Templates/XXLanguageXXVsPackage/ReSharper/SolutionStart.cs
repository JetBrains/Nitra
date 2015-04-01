using System;
using System.Diagnostics;
using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.ProjectModel;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Data.Core;
using JetBrains.VsIntegration.Shell;

namespace Nitra
{
  [SolutionComponent]
  public class SolutionStart
  {
    public SolutionStart(RawVsServiceProvider rawVsServiceProvider)
    {
      
      //var xx = ServiceProvider.GlobalProvider;
      
      Trace.Assert(false);
      var xx = (IVsDataHostService)rawVsServiceProvider.Value.GetService<IVsDataHostService, IVsDataHostService>();
      //Trace.Assert(serviceProvider != null);
      //NitraPackage.Init(serviceProvider);

      xx = xx;
    }
  }

  [ZoneMarker]
  public class ZoneMarker
  {
  }
}
