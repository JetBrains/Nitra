using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.VisualStudio.VSConstants;

namespace Nitra.VisualStudio.CodeCompletion
{
  class NitraCompletionCommandHandler : IOleCommandTarget
  {
    private IOleCommandTarget              _nextTarget;
    private IWpfTextView                   _wpfTextView;
    private NitraCompletionHandlerProvider _provider;
    private ICompletionSession             _session;

    internal NitraCompletionCommandHandler(IVsTextView textViewAdapter, IWpfTextView textView, NitraCompletionHandlerProvider provider)
    {
      _wpfTextView = textView;
      _provider = provider;

      //add the command to the command chain
      textViewAdapter.AddCommandFilter(this, out _nextTarget);
    }

    public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
    {
      if (pguidCmdGroup == VSStd2K)
      {
        switch ((VSStd2KCmdID)prgCmds[0].cmdID)
        {
          case VSStd2KCmdID.AUTOCOMPLETE:
          case VSStd2KCmdID.SHOWMEMBERLIST:
          case VSStd2KCmdID.COMPLETEWORD:
            prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
            return S_OK;
        }
      }

      return _nextTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
    }

    public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
    {
      if (VsShellUtilities.IsInAutomationFunction(_provider.ServiceProvider))
        return _nextTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

      var updateFilter = false;

      if (pguidCmdGroup == VSStd2K)
      {
        var cmd = (VSStd2KCmdID)nCmdID;

        switch (cmd)
        {
          case VSStd2KCmdID.AUTOCOMPLETE:
          case VSStd2KCmdID.COMPLETEWORD: if (StartSession()) return S_OK; break;
          case VSStd2KCmdID.RETURN:       if (TryComplete(false)) return S_OK; break;
          case VSStd2KCmdID.TAB:          if (TryComplete(true)) return S_OK; break;
          case VSStd2KCmdID.BACKSPACE:    updateFilter = true; break;
          case VSStd2KCmdID.CANCEL:       if (Cancel()) return S_OK; break;
          case VSStd2KCmdID.TYPECHAR:
            var typedChar = (char)((ushort)Marshal.GetObjectForNativeVariant(pvaIn));
            if (char.IsWhiteSpace(typedChar) || char.IsPunctuation(typedChar))
              if (!TryComplete(false))
                break;
            updateFilter = true;
            break;
        }

      }

      var result = _nextTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

      if (updateFilter)
        Filter();

      return result;
    }

    bool StartSession()
    {
      if (_session != null)
        return false;

      var caret = _wpfTextView.Caret.Position.BufferPosition;
      var snapshot = caret.Snapshot;

      _session = _provider.CompletionBroker.CreateCompletionSession(
        _wpfTextView, snapshot.CreateTrackingPoint(caret, PointTrackingMode.Positive), true);
      _session.Dismissed += _currentSession_Dismissed;
      _session.Start();

      return true;
    }

    private void _currentSession_Dismissed(object sender, EventArgs e)
    {
      _session.Dismissed -= _currentSession_Dismissed;
      _session = null;
    }

    void Filter()
    {
      if (_session == null)
        return;

      _session.SelectedCompletionSet.Filter();
      _session.SelectedCompletionSet.SelectBestMatch();
      _session.SelectedCompletionSet.Recalculate();
    }

    bool TryComplete(bool force)
    {
      if (_session == null)
        return false;

      if (!_session.SelectedCompletionSet.SelectionStatus.IsSelected && !force)
      {
        _session.Dismiss();
        return false;
      }
      else
      {
        _session.Commit();
        return true;
      }
    }

    bool Cancel()
    {
      if (_session == null)
        return false;

      _session.Dismiss();
      return true;
    }
  }
}
