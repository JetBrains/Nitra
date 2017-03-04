using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Nitra.VisualStudio.Models
{
  public class NitraErrorListProvider : ErrorListProvider, IVsTaskProvider2
  {
    public NitraErrorListProvider(IServiceProvider provider) : base(provider)
    {
    }

    int IVsTaskProvider2.MaintainInitialTaskOrder(out int fMaintainOrder)
    {
      fMaintainOrder = MaintainInitialTaskOrder ? 1 : 0;
      return 0;
    }
  }
}
