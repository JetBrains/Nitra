using JetBrains.Application;
using JetBrains.Application.changes;
using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Data.Core;

using XXNamespaceXX.ProjectSystem;

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace XXNamespaceXX
{
  [SolutionComponent]
  public class ReSharperSolution
  {
    public static XXLanguageXXSolution NitraSolution { get; private set; }

    public ReSharperSolution(Lifetime lifetime, ChangeManager changeManager, ISolution solution)
    {
      NitraSolution = new XXLanguageXXSolution(lifetime, changeManager, solution);
    }

    [ZoneMarker]
    public class ZoneMarker { }
  }
}
