using System;
using Nitra.ClientServer.Messages;
using ReactiveUI;

namespace Nitra.Visualizer.ViewModels
{
  public class PopupItemViewModel : ReactiveObject
  {
    public SymbolLocation SymbolLocation { get; private set; }
    public IntelliSensePopupViewModel Popup { get; private set; }
    public string Text { get; private set; }

    public PopupItemViewModel(string filename, NSpan span, SymbolLocation symbolLocation, IntelliSensePopupViewModel popup)
    {
      SymbolLocation = symbolLocation;
      Popup = popup;
      Text = filename + " (" + span + ")";
    }
  }
}