using System.Reactive.Linq;
using System.Windows;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Nitra.Visualizer.ViewModels
{
  public class DynamicExtensionViewModel : ReactiveObject
  {
    [Reactive]
    public bool IsEnabled { get; set; }
    public bool IsChecked { get; set; }
    public string Name { get; }

    public DynamicExtensionViewModel(string name)
    {
      Name = name;

      this.WhenAnyValue(vm => vm.IsEnabled)
          .Where(enabled => !enabled)
          .BindTo(this, vm => vm.IsChecked);
    }
  }
}