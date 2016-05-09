using System.Reactive.Linq;
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
