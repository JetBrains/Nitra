using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

using System;

using Nitra.VisualStudio.Models;
using Nitra.ClientServer.Messages;

using static Microsoft.VisualStudio.VSConstants;

using IServiceProvider = System.IServiceProvider;

namespace Nitra.VisualStudio.KeyBinding
{
  class KeyBindingCommandFilter : IOleCommandTarget, IDisposable
  {
    public bool IsAdded  { get; private set; }

    IWpfTextView      _wpfTextView;
    IOleCommandTarget _nextTarget;
    IServiceProvider  _serviceProvider;
    TextViewModel     _textViewModel;

    public KeyBindingCommandFilter(
      IWpfTextView     wpfTextView,
      IServiceProvider serviceProvider,
      TextViewModel    textViewModel
      )
    {
      _serviceProvider = serviceProvider;
      _wpfTextView     = wpfTextView;
      _textViewModel   = textViewModel;
      var path = wpfTextView.TextBuffer.GetFilePath();
      AddCommandFilter(wpfTextView.ToVsTextView());
    }

    private void AddCommandFilter(IVsTextView viewAdapter)
    {
      if (IsAdded)
        return;
        
      //get the view adapter from the editor factory
      IOleCommandTarget next;
      var hr = viewAdapter.AddCommandFilter(this, out next);

      if (hr == S_OK)
      {
        IsAdded = true;
        //you'll need the next target for Exec and QueryStatus
        if (next != null)
          _nextTarget = next;
      }
    }

    public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
    {
      if (pguidCmdGroup == GUID_VSStandardCommandSet97)
      {
        switch ((VSStd97CmdID)prgCmds[0].cmdID)
        {
          case VSStd97CmdID.GotoRef:
          case VSStd97CmdID.GotoDefn:
            return (int)OLECMDF.OLECMDF_SUPPORTED | (int)OLECMDF.OLECMDF_ENABLED;
        }
      }
      else if (pguidCmdGroup == VSStd2K)
      {
        switch ((VSStd2KCmdID)prgCmds[0].cmdID)
        {
          case VSStd2KCmdID.AUTOCOMPLETE:
          case VSStd2KCmdID.SHOWMEMBERLIST:
          case VSStd2KCmdID.COMPLETEWORD: 
            prgCmds[0].cmdf = (uint)OLECMDF.OLECMDF_ENABLED | (int)OLECMDF.OLECMDF_SUPPORTED;
            return S_OK;
        }
      }

      return _nextTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
    }

    public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
    {
      if (VsShellUtilities.IsInAutomationFunction(_serviceProvider))
        return _nextTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

      if (pguidCmdGroup == GUID_VSStandardCommandSet97)
      {
        switch ((VSStd97CmdID)nCmdID)
        {
          case VSStd97CmdID.GotoRef:  GotoRef();  return S_OK;
          case VSStd97CmdID.GotoDefn: GotoDefn(); return S_OK;
        }
      }
      else if (pguidCmdGroup == VSStd2K)
      {
        var cmd = (VSStd2KCmdID)nCmdID;
        
        switch (cmd)
        {
          case VSStd2KCmdID.GOTOBRACE:
            OnGoToBrace();
            return S_OK;
        }

      }

      var result = _nextTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
      return result;
    }

    void GotoRef()
    {
      var caretPosition = _wpfTextView.Caret.Position;
      var caretPosOpt = caretPosition.Point.GetPoint(_wpfTextView.TextBuffer, caretPosition.Affinity);
      if (caretPosOpt.HasValue)
        _textViewModel.GotoRef(caretPosOpt.Value);
    }

    void GotoDefn()
    {
      var caretPosition = _wpfTextView.Caret.Position;
      var caretPosOpt = caretPosition.Point.GetPoint(_wpfTextView.TextBuffer, caretPosition.Affinity);
      if (caretPosOpt.HasValue)
        _textViewModel.GotoDefn(caretPosOpt.Value);
    }

    void OnGoToBrace()
    {
      var brackets = _textViewModel.MatchedBrackets;
      var pairs    = brackets?.results;

      if (!pairs.HasValue || pairs.Value.IsDefaultOrEmpty)
        return;

      var caretPosition = _wpfTextView.Caret.Position;
      var caretPosOpt   = caretPosition.Point.GetPoint(_wpfTextView.TextBuffer, caretPosition.Affinity);

      if (!caretPosOpt.HasValue)
        return;

      var caretPos = caretPosOpt.Value;

      if (caretPos.Snapshot.Version.VersionNumber != brackets.Version + 1)
        return;

      var span = new NSpan(caretPos.Position, caretPos.Position);

      foreach (var pair in pairs.Value)
      {
        if (pair.Open.IntersectsWith(span))
          NavigateTo(caretPos.Snapshot, pair.Close.EndPos);
        else if (pair.Close.IntersectsWith(span))
          NavigateTo(caretPos.Snapshot, pair.Open.StartPos);
      }
    }

    void NavigateTo(ITextSnapshot snapshot, int pos)
    {
      _wpfTextView.Caret.MoveTo(new SnapshotPoint(snapshot, pos));
      _wpfTextView.ViewScroller.EnsureSpanVisible(new SnapshotSpan(snapshot, pos, 0));
    }

    public void Dispose()
    {
      var view = _wpfTextView.ToVsTextView();
      view.RemoveCommandFilter(this);
      _wpfTextView = null;
      _nextTarget = null;
      _serviceProvider = null;
      GC.SuppressFinalize(this);
    }
  }
}
