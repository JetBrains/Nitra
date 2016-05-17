using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using Nitra.Visualizer.ViewModels;
using Nitra.Visualizer.Infrastructure;
using ReactiveUI;

namespace Nitra.Visualizer.Views
{
  public partial class PopupItemView : UserControl, IViewFor<PopupItemViewModel>
  {
    public PopupItemView()
    {
      InitializeComponent();

      this.WhenActivated(disposables => {
        var events = this.Events();

        this.OneWayBind(ViewModel, vm => vm.Text, v => v.Text.Text)
            .AddTo(disposables);

        this.OneWayBind(ViewModel, vm => vm.IsDefenition, v => v.Text.FontWeight,
                        isDef => isDef ? FontWeights.Bold : FontWeights.Normal)
            .AddTo(disposables);

        events.PreviewMouseLeftButtonDown
            .InvokeCommand(ViewModel, vm => vm.Popup.Select)
            .AddTo(disposables);
      });
    }

    object IViewFor.ViewModel
    {
      get { return ViewModel; }
      set { ViewModel = (PopupItemViewModel) value; }
    }

    public PopupItemViewModel ViewModel { get; set; }
  }
}
