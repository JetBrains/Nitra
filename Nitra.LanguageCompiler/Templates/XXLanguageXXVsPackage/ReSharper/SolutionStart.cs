using JetBrains.Application;
using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.ProjectModel;
using JetBrains.VsIntegration.Shell;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Data.Core;

using System;
using System.Diagnostics;

namespace Nitra
{
  [ShellComponent]
  public class SolutionStart
  {
    public SolutionStart(/*RawVsServiceProvider rawVsServiceProvider*/)
    {
      //Trace.Assert(false);
      //var xx = (IVsDataHostService)rawVsServiceProvider.Value.GetService<IVsDataHostService, IVsDataHostService>();
    }
  }

  [ZoneMarker]
  public class ZoneMarker
  {
  }
}
