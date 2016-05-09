using System;
using System.Reactive.Linq;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Nitra.Visualizer.ViewModels;
using ReactiveUI;

namespace Nitra.Visualizer.Views
{
  public partial class IntelliSensePopup : Popup, IViewFor<IntelliSensePopupViewModel>
  {
    public IntelliSensePopup(IntelliSensePopupViewModel viewModel)
    {
      InitializeComponent();

      ViewModel = viewModel;

      var events = this.Events();

      this.Bind(ViewModel, vm => vm.IsVisible, v => v.IsOpen);

      this.OneWayBind(ViewModel, vm => vm.Items, v => v.List.ItemsSource);
      this.Bind(ViewModel, vm => vm.SelectedPopupItem, v => v.List.SelectedItem);
      
      events.KeyDown
            .Where(a => a.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            .Do(a => a.Handled = true)
            .InvokeCommand(ViewModel, vm => vm.Select);

      events.KeyDown
            .Where(a => a.Key == Key.Escape)
            .Do(a => a.Handled = true)
            .Subscribe(_ => IsOpen = false);
    }

    object IViewFor.ViewModel
    {
      get { return ViewModel; }
      set { ViewModel = (IntelliSensePopupViewModel) value; }
    }

    public IntelliSensePopupViewModel ViewModel { get; set; }
  }
}
