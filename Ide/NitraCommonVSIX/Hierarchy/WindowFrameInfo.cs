using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nitra.VisualStudio
{
  internal partial class RunningDocTableEvents
  {
    public class WindowFrameInfo : IVsWindowFrameNotify, IDisposable
    {
      readonly uint _cookie;
      readonly RunningDocTableEvents _runningDocTableEvents;
      public IVsWindowFrame WindowFrame { get; }
      public bool OnScreen { get; private set; }

      public WindowFrameInfo(IVsWindowFrame windowFrame, RunningDocTableEvents runningDocTableEvents)
      {
        ErrorHelper.ThrowIsNull(windowFrame, nameof(windowFrame));
        ThreadHelper.ThrowIfNotOnUIThread();

        _runningDocTableEvents = runningDocTableEvents;
        OnScreen = true;
        WindowFrame = windowFrame;
#pragma warning disable VSSDK002 // Visual Studio service should be used on main thread explicitly.
        var windowFrame2 = (IVsWindowFrame2)windowFrame;
        ErrorHelper.ThrowOnFailure(windowFrame2.Advise(this, out _cookie));
#pragma warning restore VSSDK002 // Visual Studio service should be used on main thread explicitly.
        _runningDocTableEvents.OnDocumentWindowOnScreenChanged(this, true);
      }

      public void Dispose()
      {
        ThreadHelper.ThrowIfNotOnUIThread();
        _runningDocTableEvents.OnDocumentWindowOnScreenChanged(this, false);
        var windowFrame2 = (IVsWindowFrame2)WindowFrame;
        ErrorHelper.ThrowOnFailure(windowFrame2.Unadvise(_cookie));
      }

      public string FullPath => WindowFrame.GetFilePath();

      public int OnShow(int fShow)
      {
        __FRAMESHOW value = (__FRAMESHOW)fShow;

        switch (value)
        {
          case __FRAMESHOW.FRAMESHOW_WinHidden:
            if (OnScreen)
            {
              const bool onScreen = false;
              OnScreen = onScreen;
              _runningDocTableEvents.OnDocumentWindowOnScreenChanged(this, onScreen);
            }
            break;
          case __FRAMESHOW.FRAMESHOW_WinShown:
            if (!OnScreen)
            {
              const bool onScreen = true;
              OnScreen = onScreen;
              _runningDocTableEvents.OnDocumentWindowOnScreenChanged(this, onScreen);
            }
            break;
        }

        return VSConstants.S_OK;
      }

      public int OnDockableChange(int fDockable)
      {
        return VSConstants.S_OK;
      }

      public int OnMove()
      {
        return VSConstants.S_OK;
      }

      public int OnSize()
      {
        return VSConstants.S_OK;
      }
    }
  }
}
