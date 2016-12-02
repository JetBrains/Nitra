using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Nitra.VisualStudio.RunningDocTableEvents;

namespace Nitra.VisualStudio
{
  internal class DocumentWindowEventArgs : EventArgs
  {
    public WindowFrameInfo Info         { get; }

    /// <summary>Record Constructor</summary>
    /// <param name="info"><see cref="Info"/></param>
    public DocumentWindowEventArgs(WindowFrameInfo info)
    {
      Info = info;
    }
  }
}
