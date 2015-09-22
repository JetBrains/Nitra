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
using JetBrains.ActionManagement;
using JetBrains.Application.CommandProcessing;
using JetBrains.DocumentManagers;
using JetBrains.TextControl.Util;
using JetBrains.UI.PopupMenu;

namespace XXNamespaceXX
{
  [SolutionComponent]
  public class ReSharperSolution
  {
    public static readonly XXLanguageXXSolution XXLanguageXXSolution = new XXLanguageXXSolution();

    public ReSharperSolution(Lifetime lifetime, IShellLocks shellLocks, ChangeManager changeManager, ISolution solution, DocumentManager documentManager, IActionManager actionManager, ICommandProcessor commandProcessor, TextControlChangeUnitFactory changeUnitFactory, JetPopupMenus jetPopupMenus)
    {
      XXLanguageXXSolution.Open(lifetime, shellLocks, changeManager, solution, documentManager, actionManager, commandProcessor, changeUnitFactory, jetPopupMenus);
    }

    [ZoneMarker]
    public class ZoneMarker { }
  }
}
