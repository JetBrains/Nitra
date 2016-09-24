using System;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Nitra.VisualStudio
{
  [DebuggerStepThrough]
  internal class ReferenceEventArgs : EventArgs
  {
    public IVsHierarchy         Hierarchy { get; }
    public uint                 ItemId    { get; }
    public VSLangProj.Reference Reference { get; }

    public ReferenceEventArgs(IVsHierarchy hierarchy, uint itemId, VSLangProj.Reference reference)
    {
      ErrorHelper.ThrowIsNull(hierarchy, nameof(hierarchy));
      ErrorHelper.ThrowIsNull(reference, nameof(reference));

      Hierarchy = hierarchy;
      ItemId    = itemId;
      Reference = reference;
    }
  }
}
