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

    private readonly TestSuiteCreateOrEditModel _dataContext;
    private readonly DispatcherTimer _timer;

    public TestSuiteDialog(bool isCreate, TestSuiteVm baseTestSuite)
    {
      _timer = new DispatcherTimer();
      _timer.Interval = TimeSpan.FromSeconds(1.3);
      _timer.Tick += _assembliesEdit_timer_Tick;

      DataContext = _dataContext = new TestSuiteCreateOrEditModel(Settings.Default, isCreate);

      InitializeComponent();

      if (baseTestSuite != null)
      {
        _dataContext.RootFolder = baseTestSuite.Solution.RootFolder;
        _dataContext.SuiteName = baseTestSuite.Name;
        _dataContext.Assemblies = string.Join(Environment.NewLine, baseTestSuite.Assemblies);

        if (baseTestSuite.Language != Nitra.Language.Instance)
          _languageComboBox.SelectedValue = baseTestSuite.Language;
      }

      _assemblies.Text = _dataContext.Assemblies;
      _assemblies.TextChanged += _assemblies_TextChanged;
    }

    public string TestSuiteName
    {
      get { return _dataContext.SuiteName; }
    }

    private void CheckBox_Changed(object sender, RoutedEventArgs e)
    {
    }

    private void _languageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private void _testSuiteName_TextChanged(object sender, TextChangedEventArgs e)
    {
    }

    private void _testSuiteName_KeyUp(object sender, KeyEventArgs e)
    {
      //if (e.Key == Key.F5)
      //  UpdateName();
    }

    private void _testSuiteName_LostFocus(object sender, RoutedEventArgs e)
    {
      //if (string.IsNullOrWhiteSpace(_testSuiteName.Text))
      //  UpdateName();
    }

    private void _assemblies_TextChanged(object sender, TextChangedEventArgs e)
    {
      _timer.Stop();
      _timer.Start();
    }

    void _assembliesEdit_timer_Tick(object sender, EventArgs e)
    {
      _timer.Stop();
      _dataContext.Assemblies = _assemblies.Text;
    }

    private void _assemblies_LostFocus(object sender, RoutedEventArgs e)
    {
      _timer.Stop();
      _dataContext.Assemblies = _assemblies.Text;
      _assemblies.Text = _dataContext.NormalizedAssembliesText;
    }

    private void _assemblies_KeyUp(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.F5)
      {
        _dataContext.Assemblies = _assemblies.Text;
        _assemblies.Text = _dataContext.NormalizedAssembliesText;
      }
    }

    private void _okButton_Click(object sender, RoutedEventArgs e)
    {
      //var testSuiteName = _testSuiteName.Text;

      //if (string.IsNullOrWhiteSpace(testSuiteName))
      //{
      //  MessageBox.Show(this, "Name of test suite can't be empty.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
      //  _testSuiteName.Focus();
      //  return;
      //}

      //var root = Path.GetFullPath(_rootFolder);
      //var path = Path.Combine(root, testSuiteName);

      //if (Directory.Exists(path) && _create)
      //{
      //  MessageBox.Show(this, "The test suite '" + testSuiteName + "' already exists.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
      //  _testSuiteName.Focus();
      //  return;
      //}

      //if (Utils.IsInvalidDirName(testSuiteName))
      //{
      //  MessageBox.Show(this, "Name of test suite is invalid.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
      //  _testSuiteName.Focus();
      //  return;
      //}

      //MakeAllPathsRelative();

      //var assemblyPaths = Utils.GetAssemblyPaths(_assemblies.Text);

      //if (assemblyPaths.Length == 0)
      //{
      //  MessageBox.Show(this, "No one valid library in library list.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
      //  _assemblies.Focus();
      //  return;
      //}

      //var syntaxModules = GetSelectedGrammarDescriptor();

      //if (syntaxModules.Length == 0)
      //{
      //  MessageBox.Show(this, "No syntax module is selected.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
      //  _syntaxModules.Focus();
      //  return;
      //}

      //var startRule = _startRuleComboBox.SelectedItem as RuleDescriptor;

      //if (startRule == null)
      //{
      //  MessageBox.Show(this, "No a start rule is selected.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
      //  _syntaxModules.Focus();
      //  return;
      //}

      //try
      //{
      //  if (_baseTestSuite != null && _baseTestSuite.Name != testSuiteName && Directory.Exists(_baseTestSuite.FullPath))
      //  {
      //    Directory.CreateDirectory(path);
      //    FileSystem.CopyDirectory(_baseTestSuite.FullPath, path, UIOption.AllDialogs);
      //    Directory.Delete(_baseTestSuite.FullPath, recursive: true);
      //  }
      //  else
      //    Directory.CreateDirectory(path);

      //  var xml = Utils.MakeXml(root, syntaxModules, startRule, _languageName.Text);
      //  var configPath = Path.Combine(path, "config.xml");
      //  xml.Save(configPath);
      //  TestSuiteName = testSuiteName;
      //}
      //catch (Exception ex)
      //{
      //  MessageBox.Show(this, ex.GetType().Name + ":" + ex.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
      //  return;
      //}

      this.DialogResult = true;
      Close();
    }

    private void _addLibButton_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new OpenFileDialog
      {
        DefaultExt = ".dll",
        InitialDirectory = _dataContext.RootFolder,
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
