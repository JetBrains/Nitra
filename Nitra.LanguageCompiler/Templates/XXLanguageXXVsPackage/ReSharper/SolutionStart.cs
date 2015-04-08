using JetBrains.Application;
using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.ProjectModel;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Data.Core;

using System;
using System.Diagnostics;

namespace Nitra
{
  [SolutionComponent]
  public class SolutionStart
  {
    public SolutionStart()
    {
    }
  }

  [ZoneMarker]
  public class ZoneMarker
  {
  }
}
