using System.Windows;
using Nitra.Visualizer.Properties;
using ReactiveUI;

namespace Nitra.Visualizer
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application
  {
    private void Application_Startup(object sender, StartupEventArgs e)
    {
      // Unfortunately, in year 2016 WPF still doesn't support multiple item notification for list controls.
      // Means if you call AddRange, don't forget to call Reset to send Changed notification manually.
      RxApp.SupportsRangeNotifications = false;
      Settings.Default.Reload();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
      Settings.Default.Save();
    }
  }
}
