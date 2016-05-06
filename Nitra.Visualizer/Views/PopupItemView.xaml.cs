using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Nitra.ClientServer.Messages;
using Nitra.Visualizer.ViewModels;
using ReactiveUI;

namespace Nitra.Visualizer.Views
{
  public partial class PopupItemView : UserControl, IViewFor<PopupItemViewModel>
  {
    public PopupItemView()
    {
      InitializeComponent();

      this.WhenActivated(d => {
        d(this.OneWayBind(ViewModel, vm => vm.Text, v => v.Text.Text));

        var events = this.Events();
        
        d(events.PreviewMouseLeftButtonDown
                .InvokeCommand(ViewModel, vm => vm.Select));
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
