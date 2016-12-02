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
  internal partial class RunningDocTableEvents : IVsRunningDocTableEvents, IVsRunningDocTableEvents2, IVsRunningDocTableEvents3, IVsRunningDocTableEvents4, IDisposable
  {
    readonly RunningDocumentTable  _runningDocumentTable;
    readonly uint                  _coockie;
    readonly List<WindowFrameInfo>    _activeFrames = new List<WindowFrameInfo>();
    readonly Dictionary<IVsWindowFrame, WindowFrameInfo> _windowFrames = new Dictionary<IVsWindowFrame, WindowFrameInfo> ();

    public RunningDocumentTable RunningDocumentTable { get { return _runningDocumentTable; } }

    public event EventHandler<DocumentWindowOnScreenChangedEventArgs> DocumentWindowOnScreenChanged;
    public event EventHandler<DocumentWindowEventArgs>                DocumentWindowCreate;
    public event EventHandler<DocumentWindowEventArgs>                DocumentWindowDestroy;

    public RunningDocTableEvents()
    {
      _runningDocumentTable = new RunningDocumentTable();
      _coockie = _runningDocumentTable.Advise(this);
    }

    public void Dispose()
    {
      _runningDocumentTable.Unadvise(_coockie);
    }

    private void OnDocumentWindowOnScreenChanged(WindowFrameInfo info, bool onScreen)
    {
      Debug.WriteLine($"tr: OnScreen={onScreen}, WindowFrame='{info.WindowFrame}'");

      if (onScreen)
        _activeFrames.Add(info);
      else
        _activeFrames.Remove(info);


      foreach (var activeFrame in _activeFrames)
        Debug.WriteLine($"tr:   OnScreen='{activeFrame.OnScreen}', path='{activeFrame.FullPath}')");

      DocumentWindowOnScreenChanged?.Invoke(null, new DocumentWindowOnScreenChangedEventArgs(info, onScreen));
    }

    public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame frame)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      if (fFirstShow != 0)
      {
        var windowFrameInfo = new WindowFrameInfo(frame, this);
        _windowFrames.Add(frame, windowFrameInfo);
        DocumentWindowCreate?.Invoke(this, new DocumentWindowEventArgs(windowFrameInfo));
      }

      //var path = frame.GetFilePath();
      //int isOnScreen;
      //frame.IsOnScreen(out isOnScreen);
      //Debug.WriteLine($"tr: BeforeDocumentWindowShow(docCookie={docCookie}, fFirstShow='{fFirstShow != 0}', isOnScreen={isOnScreen}, path='{path}')");


      //if (_activeFrames.Count == 0 || _activeFrames[0] != frame)
      //{
      //  _activeFrames.Remove(frame);
      //  _activeFrames.Insert(0, frame);
      //}
      //
      // посылаем сообщение активации
      return VSConstants.S_OK;
    }

    public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame frame)
    {
      ThreadHelper.ThrowIfNotOnUIThread();

      //object path = frame.GetFilePath();
      //Debug.WriteLine($"tr: OnAfterDocumentWindowHide(docCookie={docCookie}, path='{path}')");

      WindowFrameInfo windowFrameInfo;

      if (_windowFrames.TryGetValue(frame, out windowFrameInfo))
      {
        DocumentWindowDestroy?.Invoke(this, new DocumentWindowEventArgs(windowFrameInfo));
        windowFrameInfo.Dispose();
        _windowFrames.Remove(frame);
      }

      return VSConstants.S_OK;
    }

    public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
    {
      return VSConstants.S_OK;
    }

    public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
    {
      return VSConstants.S_OK;
    }

    public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
    {
      return VSConstants.S_OK;
    }

    public int OnAfterLastDocumentUnlock(IVsHierarchy pHier, uint itemid, string pszMkDocument, int fClosedWithoutSaving)
    {
      return VSConstants.S_OK;
    }

    public int OnAfterSave(uint docCookie)
    {
      return VSConstants.S_OK;
    }

    public int OnAfterSaveAll()
    {
      return VSConstants.S_OK;
    }

    public int OnBeforeFirstDocumentLock(IVsHierarchy pHier, uint itemid, string pszMkDocument)
    {
      return VSConstants.S_OK;
    }

    public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
    {
      return VSConstants.S_OK;
    }

    public int OnBeforeSave(uint docCookie)
    {
      return VSConstants.S_OK;
    }
  }
}
