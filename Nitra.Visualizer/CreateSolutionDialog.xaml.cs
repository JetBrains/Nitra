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
using Nitra.Visualizer.Properties;

namespace Nitra.Visualizer
{
  /// <summary>
  /// Interaction logic for TestsSettingsWindow.xaml
  /// </summary>
  public partial class CreateSolutionDialog : Window
  {
    Settings _settings;

    public string SolutionFilePath { get; set; }

    public CreateSolutionDialog()
    {
      InitializeComponent();

      _settings = Settings.Default;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      var dir = _settings.CurrentSolution ?? "";
      if (Directory.Exists(dir))
        _testsLocationRootTextBox.Text = dir;
      else
        _testsLocationRootTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Visualizer");
    }

    bool Validate()
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
        var res = MessageBox.Show(this, "Path '" + testsLocationRootFull + "' does not exits. Create it?", Constants.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (res == MessageBoxResult.No)
        {
          _testsLocationRootTextBox.Focus();
          return false;
        }

        try
        {
          Directory.CreateDirectory(testsLocationRootFull);
          SolutionFilePath = testsLocationRoot;
          return true;
        }
        catch (Exception ex)
        {
          MessageBox.Show(this, "Can't create the folder '" + testsLocationRootFull + "'.\r\n" + ex.GetType().Name + ":" + ex.Message, Constants.AppName, MessageBoxButton.OK,
            MessageBoxImage.Error);
          _testsLocationRootTextBox.Focus();
          return false;
        }
      }

      var solutionFilePath = Path.Combine(testsLocationRoot, _solutionName.Name, ".nsln");

      if (File.Exists(solutionFilePath))
      {
        MessageBox.Show(this, "The file '" + solutionFilePath + "' already exists!", Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        _solutionName.Focus();
        return false;
      }

      try
      {
        File.WriteAllText(solutionFilePath, "");
        File.Delete(solutionFilePath);
      }
      catch (Exception ex)
      {
        MessageBox.Show(this, "Can't create the file '" + solutionFilePath + "'.\r\n" + ex.GetType().Name + ":" + ex.Message, Constants.AppName, MessageBoxButton.OK,
          MessageBoxImage.Error);
        _solutionName.Focus();
        return false;
      }

      SolutionFilePath = testsLocationRoot;
      return true;
    }

    private void _okButton_Click(object sender, RoutedEventArgs e)
    {
      if (Validate())
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
        _testsLocationRootFullPathTextBlock.Text = ex.GetType().Name + ":" + ex.Message;
        SetRedColor();
      }
    }

    private void _solutionName_TextChanged(object sender, TextChangedEventArgs e)
    {
      var name = _solutionName.Text;
      if (string.IsNullOrWhiteSpace(name))
      {
        _testsLocationRootFullPathTextBlock.Text = "Solution name can't be empty!";
        SetRedColor();
        return;
      }

      var index = name.IndexOfAny(Path.GetInvalidFileNameChars());
      if (index >= 0)
      {
        _testsLocationRootFullPathTextBlock.Text = "Solution name can't contain the character '" + name[index] + "'!";
        SetRedColor();
        return;
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
