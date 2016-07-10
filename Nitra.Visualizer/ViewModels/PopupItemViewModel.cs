using System;
using Nitra.ClientServer.Messages;
using ReactiveUI;
using Nitra.ViewModels;

namespace Nitra.Visualizer.ViewModels
{
  public class PopupItemViewModel : ReactiveObject
  {
    public int                        SymbolId     { get; private set; }
    public FileVm                     File         { get; private set; }
    public NSpan                      Span         { get; private set; }
    public bool                       IsDefenition { get; private set; }
    public IntelliSensePopupViewModel Popup        { get; private set; }
    public string                     Text         { get; private set; }

    public PopupItemViewModel(int symbolId, FileVm file, NSpan span, bool isDefenition, IntelliSensePopupViewModel popup)
    {
      SymbolId     = symbolId;
      File         = file;
      Span         = span;
      IsDefenition = isDefenition;
      Text         = file.Name + " (" + span + ")"; // TODO: convert to line pos and make line preview
      Popup        = popup;
    }
  }
}