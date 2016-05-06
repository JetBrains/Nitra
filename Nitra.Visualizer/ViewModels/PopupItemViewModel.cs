using System;
using Nitra.ClientServer.Messages;
using ReactiveUI;

namespace Nitra.Visualizer.ViewModels
{
  public class PopupItemViewModel : ReactiveObject
  {
    public SymbolLocation SymbolLocation { get; set; }
    public string Text { get; set; }

    public IReactiveCommand<object> Select { get; }

    public PopupItemViewModel(string filename, NSpan span, SymbolLocation symbolLocation, NitraTextEditorViewModel editor)
    {
      SymbolLocation = symbolLocation;
      Text = filename + " (" + span + ")";

      Select = ReactiveCommand.Create();
      Select.Subscribe(_ => {
        editor.SelectText(SymbolLocation.Location);
        editor.PopupVisible = false;
      });
    }
  }
}