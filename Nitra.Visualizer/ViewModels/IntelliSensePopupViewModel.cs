using System;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Nitra.Visualizer.ViewModels
{
  public class IntelliSensePopupViewModel : ReactiveObject
  {
    [Reactive] public bool IsVisible { get; set; }
    [Reactive] public PopupItemViewModel SelectedPopupItem { get; set; }

    public ReactiveList<PopupItemViewModel> Items { get; set; }
    public IReactiveCommand<object> Select { get; private set; }
    
    public IntelliSensePopupViewModel(NitraTextEditorViewModel editor)
    {
      Items = new ReactiveList<PopupItemViewModel>();

      var canSelect = this.WhenAny(v => v.SelectedPopupItem, item => item.Value != null);

      Select = ReactiveCommand.Create(canSelect);
      Select.Subscribe(_ => {
        editor.SelectText(SelectedPopupItem.File, SelectedPopupItem.Span);

        IsVisible = false;
        SelectedPopupItem = null;
      });
    }
  }
}