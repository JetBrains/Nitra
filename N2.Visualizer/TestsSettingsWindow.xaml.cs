using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
//using System.Windows.Shapes;
using N2.Visualizer.Properties;

namespace N2.Visualizer
{
  /// <summary>
  /// Interaction logic for TestsSettingsWindow.xaml
  /// </summary>
  public partial class TestsSettingsWindow : Window
  {
    Settings _settings;

    public string TestsLocationRoot { get; set; }

    public TestsSettingsWindow()
    {
      InitializeComponent();

      _settings = Settings.Default;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      _testsLocationRootTextBox.Text = _settings.TestsLocationRoot;
    }

    bool Validate_TestsLocationRoot()
    {
      var testsLocationRoot = _testsLocationRootTextBox.Text;

      if (string.IsNullOrWhiteSpace(testsLocationRoot))
      {
        MessageBox.Show(this, "You must specify correct path to a test folder.");
        _testsLocationRootTextBox.Focus();
        return false;
      }

      var testsLocationRootFull = Path.GetFullPath(testsLocationRoot);

      if (!Directory.Exists(testsLocationRootFull))
      {
        MessageBox.Show(this, "Path '" + testsLocationRootFull + "' does not exits.");
        _testsLocationRootTextBox.Focus();
        return false;
      }

      TestsLocationRoot = testsLocationRoot;
      return true;
    }

    private void _okButton_Click(object sender, RoutedEventArgs e)
    {
      if (Validate_TestsLocationRoot())
      {
        e.Handled = true;
        DialogResult = true;
        Close();
      }
    }

    private void _testsLocationRootTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
      var fullPath = Path.GetFullPath(_testsLocationRootTextBox.Text ?? "");
      _testsLocationRootFullPathTextBlock.Text = fullPath;
      if (Directory.Exists(fullPath))
      {
        _testsLocationRootFullPathTextBlock.Opacity = 0.4;
        _testsLocationRootFullPathTextBlock.Foreground = new SolidColorBrush { Color = Colors.Black };
      }
      else
      {
        _testsLocationRootFullPathTextBlock.Opacity = 1.0;
        _testsLocationRootFullPathTextBlock.Foreground = new SolidColorBrush { Color = Colors.Red };
      }
    }
  }
}
