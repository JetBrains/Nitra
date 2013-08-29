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
      var dir = _settings.TestsLocationRoot ?? "";
      if (Directory.Exists(dir))
        _testsLocationRootTextBox.Text = dir;
      else
        _testsLocationRootTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Visualizer");
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
        var res = MessageBox.Show(this, "Path '" + testsLocationRootFull + "' does not exits. Create it?", "Visualizer", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (res == MessageBoxResult.No)
        {
          _testsLocationRootTextBox.Focus();
          return false;
        }

        try
        {
          Directory.CreateDirectory(testsLocationRootFull);
          TestsLocationRoot = testsLocationRoot;
          return true;
        }
        catch (Exception ex)
        {
          MessageBox.Show(this, "Can't create the folder '" + testsLocationRootFull + "'.\r\n" + ex.Message, "Visualizer", MessageBoxButton.OK,
            MessageBoxImage.Error);
          _testsLocationRootTextBox.Focus();
          return false;
        }
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
      try
      {
        var fullPath = Path.GetFullPath(_testsLocationRootTextBox.Text ?? "");
        _testsLocationRootFullPathTextBlock.Text = fullPath;
        if (Directory.Exists(fullPath))
        {
          _testsLocationRootFullPathTextBlock.Opacity = 0.4;
          _testsLocationRootFullPathTextBlock.Foreground = new SolidColorBrush { Color = Colors.Black };
        }
        else
          SetRedColor();
      }
      catch (Exception ex)
      {
        _testsLocationRootFullPathTextBlock.Text = "Error: " + ex.Message;
        SetRedColor();
      }
    }

    private void SetRedColor()
    {
      _testsLocationRootFullPathTextBlock.Opacity = 1.0;
      _testsLocationRootFullPathTextBlock.Foreground = new SolidColorBrush {Color = Colors.Red};
    }

    private void _chooseFolderButton_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new System.Windows.Forms.FolderBrowserDialog();
      dialog.Description = "Select root folder for Nitra Visualizer tests.";
      dialog.SelectedPath = _testsLocationRootTextBox.Text;
      dialog.ShowNewFolderButton = true;
      if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
      {
        _testsLocationRootTextBox.Text = dialog.SelectedPath;
      }
    }
  }
}
