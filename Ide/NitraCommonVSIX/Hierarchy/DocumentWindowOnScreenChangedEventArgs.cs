using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Nitra.VisualStudio.RunningDocTableEvents;

namespace Nitra.VisualStudio
{
  internal class DocumentWindowOnScreenChangedEventArgs : EventArgs
  {
    public WindowFrameInfo Info     { get; }
    public bool            OnScreen { get; }

    /// <summary>Record Constructor</summary>
    /// <param name="info"><see cref="Info"/></param>
    /// <param name="onScreen"><see cref="OnScreen"/></param>
    public DocumentWindowOnScreenChangedEventArgs(WindowFrameInfo info, bool onScreen)
    {
      Info = info;
      OnScreen = onScreen;
    }
  }
}
