using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;

using Nitra.Visualizer.Properties;
using Nitra.ViewModels;


using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;


namespace Nitra.Visualizer
{
  internal partial class TestSuiteDialog
  {
    const string _assembiesToolTip   = "Enter paths to assemblies (one assembly per line)";

    private readonly TestSuiteCreateOrEditModel _model;
    private readonly DispatcherTimer _timer;
    private readonly TestSuiteVm _baseTestSuite;

    public TestSuiteDialog(bool isCreate, TestSuiteVm baseTestSuite, Settings settings)
    {
      _baseTestSuite = baseTestSuite;

      _timer = new DispatcherTimer();
      _timer.Interval = TimeSpan.FromSeconds(1.3);
      _timer.Tick += _assembliesEdit_timer_Tick;

      DataContext = _model = new TestSuiteCreateOrEditModel(settings, isCreate);

      InitializeComponent();

      if (baseTestSuite != null)
      {
        _model.RootFolder = baseTestSuite.Solution.RootFolder;
        _model.SuiteName = baseTestSuite.Name;
        _model.NormalizedAssemblies = baseTestSuite.Assemblies;

        if (baseTestSuite.Language != Nitra.Language.Instance)
          _model.SelectedLanguage = baseTestSuite.Language;
      }

      _assemblies.Text = _model.NormalizedAssembliesText;
      _assemblies.TextChanged += _assemblies_TextChanged;
    }

    public string TestSuiteName
    {
      get { return _model.SuiteName; }
    }

    private void _assemblies_TextChanged(object sender, TextChangedEventArgs e)
    {
      _timer.Stop();
      _timer.Start();
    }

    void _assembliesEdit_timer_Tick(object sender, EventArgs e)
    {
      _timer.Stop();
      _model.Assemblies = _assemblies.Text;
    }

    private void _assemblies_LostFocus(object sender, RoutedEventArgs e)
    {
      _timer.Stop();
      _model.Assemblies = _assemblies.Text;
      _assemblies.Text = _model.NormalizedAssembliesText;
    }

    private void _assemblies_KeyUp(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.F5)
      {
        _model.Assemblies = _assemblies.Text;
        _assemblies.Text = _model.NormalizedAssembliesText;
      }
    }

    private void _okButton_Click(object sender, RoutedEventArgs e)
    {
      var testSuiteName = TestSuiteName;

      if (string.IsNullOrWhiteSpace(testSuiteName))
      {
        MessageBox.Show(this, "Name of test suite can't be empty.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _testSuiteName.Focus();
        return;
      }

      var root = Path.GetFullPath(_model.RootFolder);
      var path = Path.Combine(root, testSuiteName);

      if (Directory.Exists(path) && _model.IsCreate)
      {
        MessageBox.Show(this, "The test suite '" + testSuiteName + "' already exists.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _testSuiteName.Focus();
        return;
      }

      if (Utils.IsInvalidDirName(testSuiteName))
      {
        MessageBox.Show(this, "Name of test suite is invalid.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _testSuiteName.Focus();
        return;
      }

      var assemblies = _model.NormalizedAssemblies;

      if (assemblies.Length == 0)
      {
        MessageBox.Show(this, "No one valid library in library list.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _assemblies.Focus();
        return;
      }

      var selectedLanguage = _model.SelectedLanguage;

      if (selectedLanguage == null)
      {
        MessageBox.Show(this, "Langauge is not selected.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _languageComboBox.Focus();
        return;
      }

      try
      {
        Directory.CreateDirectory(path);
        if (_baseTestSuite != null && _baseTestSuite.Name != testSuiteName && Directory.Exists(_baseTestSuite.FullPath))
        {
          FileSystem.CopyDirectory(_baseTestSuite.FullPath, path, UIOption.AllDialogs);
          Directory.Delete(_baseTestSuite.FullPath, recursive: true);
        }

        var dynamicExtensions = _model.DynamicExtensions.Where(x => x.IsEnabled && x.IsChecked).Select(x => x.Descriptor);
        var xml               = Utils.MakeXml(root, selectedLanguage, dynamicExtensions);
        var configPath        = Path.Combine(path, TestSuiteVm.ConfigFileName);
        File.WriteAllText(configPath, xml);
      }
      catch (Exception ex)
      {
        MessageBox.Show(this, ex.GetType().Name + ":" + ex.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

      this.DialogResult = true;
      Close();
    }

    private void _addLibButton_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new OpenFileDialog
      {
        DefaultExt = ".dll",
        InitialDirectory = _model.RootFolder,
        Filter = "Parser library (.dll)|*.dll|Parser application (.exe)|*.exe",
        Title = "Load parser"
      };

      if (dialog.ShowDialog(this) ?? false)
      {
        _assemblies.Text += Environment.NewLine + dialog.FileName;
        _assemblies.Focus();
      }
    }
  }
}
