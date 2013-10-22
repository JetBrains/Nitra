using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows;
using Nitra.Visualizer.Properties;

namespace Nitra.Visualizer
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application
  {
    private void Application_Startup(object sender, StartupEventArgs e)
    {
      Settings.Default.Reload();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
      Settings.Default.Save();
    }
  }
}
