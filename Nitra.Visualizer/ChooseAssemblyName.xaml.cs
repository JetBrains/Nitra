using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Nitra.Visualizer
{
  /// <summary>
  /// Interaction logic for ChooseAssemblyName.xaml
  /// </summary>
  public partial class ChooseAssemblyName : Window
  {
    public ChooseAssemblyName(AssemblyName assemblyName = null)
    {
      InitializeComponent();

      if (assemblyName != null)
        _assemblyName.Text = assemblyName.ToString();
    }

    public AssemblyName AssemblyName { get; set; }

    private void _okButton_Click(object sender, RoutedEventArgs e)
    {
      var name = _assemblyName.Text;
      try
      {
        var assemblyName = new AssemblyName(name);
        if (assemblyName.Version == null)
        {
          MessageBox.Show(this, "Version is not specified.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
          return;
        }
        AssemblyName = assemblyName;
        this.DialogResult = true;
        Close();
      }
      catch (Exception ex)
      {
        MessageBox.Show(this, "Wrong assembly name\r\nException: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void _cancelButton_Click(object sender, RoutedEventArgs e)
    {
      Close();
    }
  }
}
