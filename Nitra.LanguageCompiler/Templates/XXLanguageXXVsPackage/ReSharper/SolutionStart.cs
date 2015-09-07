using JetBrains.Application;
using JetBrains.Application.changes;
using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Data.Core;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using XXNamespaceXX.ProjectSystem;

namespace XXNamespaceXX
{
  [SolutionComponent]
  public class SolutionStart
  {
    private readonly XXLanguageXXSolution _solution;

    public SolutionStart(Lifetime lifetime, ChangeManager changeManager, ISolution solution)
    {
      _solution = new XXLanguageXXSolution(lifetime, changeManager, solution);
    }
  }

  [ZoneMarker]
  public class ZoneMarker
  {
  }
}
