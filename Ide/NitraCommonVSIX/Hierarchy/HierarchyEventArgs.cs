using System;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Nitra.VisualStudio
{
  [DebuggerStepThrough]
  internal class HierarchyItemEventArgs : EventArgs
  {
    public IVsHierarchy Hierarchy { get; }
    public uint         ItemId    { get; }
    public string       FileName  { get; }

    public HierarchyItemEventArgs(IVsHierarchy hierarchy, uint itemId, string fileName)
    {
      ErrorHelper.ThrowIsNull(hierarchy, nameof(hierarchy));
      ErrorHelper.ThrowIsNullOrEmpty(fileName, nameof(fileName));

      Hierarchy = hierarchy;
      ItemId    = itemId;
      FileName  = fileName;
    }
  }
}
