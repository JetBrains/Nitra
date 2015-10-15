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

    public string TestSuiteName { get; private set; }

    readonly Settings _settings;
    bool _nameUpdate;
    bool _nameChangedByUser;
    readonly bool _create;
    private string _rootFolder;
    private TestSuiteVm _baseTestSuite;

    readonly DispatcherTimer _timer = new DispatcherTimer();

    public TestSuiteDialog(bool create, TestSuiteVm baseTestSuite)
    {
      _settings      = Settings.Default;
      _create        = create;
      _baseTestSuite = baseTestSuite;

      InitializeComponent();

      this.Title = create ? "New test suite" : "Edit test suite";

      var root = baseTestSuite != null ? baseTestSuite.Solution.RootFolder : Path.GetDirectoryName(_settings.CurrentSolution);
      _rootFolder = root;
      _testsRootTextBlock.Text = root;
      var paths =
        baseTestSuite == null
        ? "" 
        : string.Join(Environment.NewLine,
        baseTestSuite.SynatxModules.Select(m =>
          Utils.MakeRelativePath(@from: root, isFromDir: true, to: m.GetType().Assembly.Location, isToDir: false)).Distinct());
      _assemblies.Text = paths;

      if (!create)
      {
        if (string.IsNullOrWhiteSpace(paths))
        {
          _assemblies.Text = paths = string.Join(Environment.NewLine, baseTestSuite != null ? baseTestSuite.Assemblies : new string[0]);
        }
        this._testSuiteName.Text = baseTestSuite == null ? "" : baseTestSuite.Name;
      }

      UpdateSyntaxModules(paths, root);

      var items = (ObservableCollection<SyntaxModuleVm>)_syntaxModules.ItemsSource;
      if (baseTestSuite != null)
      {
        var selectedSynatxModules = baseTestSuite.SynatxModules.Join(items, d => d, suite => suite.GrammarDescriptor, (d, suite) => suite);
        foreach (var selectedSynatxModule in selectedSynatxModules)
          selectedSynatxModule.IsChecked = true;
      }

      UpdateStartRules(baseTestSuite == null || baseTestSuite.StartRule == null || baseTestSuite.StartRule.IsStartRule);

      if (baseTestSuite != null && baseTestSuite.StartRule != null)
      {
        foreach (var x in _startRuleComboBox.ItemsSource)
          if (x == baseTestSuite.StartRule)
            _startRuleComboBox.SelectedItem = x;
      }

      _timer.Interval = TimeSpan.FromSeconds(1.3);
      _timer.Stop();
      _timer.Tick += _assembliesEdit_timer_Tick;
    }

    private void UpdateSyntaxModules(string relativeAssemblyPaths, string testsLocationRootFullPath)
    {
      var syntaxModules = new List<SyntaxModuleVm>();

      try
      {
        foreach (var relativeAssemblyPath in Utils.GetAssemblyPaths(relativeAssemblyPaths))
        {
          var path = Path.Combine(testsLocationRootFullPath, relativeAssemblyPath);
          var fullPath = Path.GetFullPath(path);
          var grammarDescriptors = Utils.LoadAssembly(fullPath, _settings.Config);
          foreach (var grammarDescriptor in grammarDescriptors)
            syntaxModules.Add(new SyntaxModuleVm(grammarDescriptor));
        }

        _assemblies.Foreground = Brushes.Black;
        _assemblies.ToolTip = _assembiesToolTip;
      }
      catch (Exception ex)
      {
        syntaxModules.Clear();
        _assemblies.Foreground = Brushes.Red;
        _assemblies.ToolTip = ex.GetType().Name + ":" + ex.Message + Environment.NewLine + _assembiesToolTip;
      }

      var prevItems = _syntaxModules.ItemsSource as ObservableCollection<SyntaxModuleVm>;
      var newItems = new ObservableCollection<SyntaxModuleVm>(SortSyntaxModules(syntaxModules));

      if (prevItems != null && Enumerable.SequenceEqual(prevItems, newItems))
        return;

      _syntaxModules.ItemsSource = newItems;
      UpdateStartRules(true);
    }

    private static IEnumerable<SyntaxModuleVm> SortSyntaxModules(IEnumerable<SyntaxModuleVm> syntaxModules)
    {
      return syntaxModules.OrderBy(m => m.Name);
    }

    private string MakeName()
    {
      var start = _startRuleComboBox.SelectedItem as RuleDescriptor;

      string name = null;
      var syntaxModules = (ObservableCollection<SyntaxModuleVm>)_syntaxModules.ItemsSource;
      foreach (var syntaxModule in syntaxModules)
        if (syntaxModule.IsChecked)
        {
          if (name != null)
            name += "-";

          if (start != null && start.Grammar == syntaxModule.GrammarDescriptor)
            name += syntaxModule.Name + "~" + start.Name;
          else
            name += syntaxModule.Name;
        }

      return name;
    }

    private void UpdateStartRules(bool showOnlyStratRules)
    {
      var oldSelected = _startRuleComboBox.SelectedItem;

      var rules = new List<object>();
      var syntaxModules = (ObservableCollection<SyntaxModuleVm>)_syntaxModules.ItemsSource;
      foreach (var syntaxModule in syntaxModules)
        if (syntaxModule.IsChecked)
        {
          foreach (var rule in GetRules(syntaxModule))
            if (rule.IsStartRule || !showOnlyStratRules)
              rules.Add(rule);
        }

      if (rules.Count > 0)
      {
        if (oldSelected == null)
          _startRuleComboBox.SelectedItem = rules[0];
        else
        {
          var index = rules.IndexOf(oldSelected);
          _startRuleComboBox.SelectedItem = index >= 0 ? rules[index] : rules[0];
        }

        rules.Insert(0, showOnlyStratRules ? _showAllRules : _showOnlyStartRules);
      }
      else if (showOnlyStratRules)
        rules.Insert(0, _showAllRules);

      _startRuleComboBox.ItemsSource = rules;

      if (string.IsNullOrWhiteSpace(_testSuiteName.Text) || !_nameChangedByUser)
        UpdateName();
    }

    private void UpdateName()
    {
      _nameChangedByUser = false;
      _nameUpdate = true;
      try
      {
        _testSuiteName.Text = MakeName();
      }
      finally
      {
        _nameUpdate = false;
      }
    }

    private GrammarDescriptor[] GetSelectedGrammarDescriptor()
    {
      var syntaxModules = _syntaxModules.ItemsSource as ObservableCollection<SyntaxModuleVm>;

      var x = syntaxModules == null
        ? new GrammarDescriptor[0]
        : syntaxModules.Where(m => m.IsChecked).Select(m => m.GrammarDescriptor).ToArray();

      return x;
    }

    private void CheckBox_Changed(object sender, RoutedEventArgs e)
    {
      UpdateStartRules(true);
    }

    private void _languageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
// ReSharper disable RedundantCast
      if (_languageComboBox.SelectedItem == (object)_showAllRules)
        UpdateStartRules(false);
      else if (_languageComboBox.SelectedItem == (object)_showOnlyStartRules)
        UpdateStartRules(true);
// ReSharper restore RedundantCast

      if (string.IsNullOrWhiteSpace(_testSuiteName.Text) || !_nameChangedByUser)
        UpdateName();
    }

    private void _testSuiteName_TextChanged(object sender, TextChangedEventArgs e)
    {
      if (_nameUpdate)
        return;
      _nameChangedByUser = true;
    }

    private void _testSuiteName_KeyUp(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.F5)
        UpdateName();
    }

    private void _testSuiteName_LostFocus(object sender, RoutedEventArgs e)
    {
      if (string.IsNullOrWhiteSpace(_testSuiteName.Text))
        UpdateName();
    }

    private void _assemblies_TextChanged(object sender, TextChangedEventArgs e)
    {
      _timer.Stop();
      _timer.Start();
    }

    void _assembliesEdit_timer_Tick(object sender, EventArgs e)
    {
      _timer.Stop();
      UpdateSyntaxModules(_assemblies.Text, Path.GetFullPath(_rootFolder));
    }

    private void MakeAllPathsRelative()
    {
      var assemblyPaths = _assemblies.Text.Trim('\n', '\r', '\t', ' ');
      var result = new List<string>();
      var testsLocationRootFullPath = Path.GetFullPath(_rootFolder);
      foreach (var assemblyPath in Utils.GetAssemblyPaths(assemblyPaths))
      {
        var path = Path.Combine(testsLocationRootFullPath, assemblyPath.Trim());
        var fullPath = Path.GetFullPath(path);
        var relativeAssemblyPath = Utils.MakeRelativePath(testsLocationRootFullPath, true, fullPath, false);
        if (!string.IsNullOrWhiteSpace(relativeAssemblyPath))
          result.Add(relativeAssemblyPath);
      }

      var text = string.Join(Environment.NewLine, result);

      if (assemblyPaths != text)
        _assemblies.Text = string.Join(Environment.NewLine, result);
    }

    private void _assemblies_LostFocus(object sender, RoutedEventArgs e)
    {
      MakeAllPathsRelative();
    }

    private void _assemblies_KeyUp(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.F5)
        MakeAllPathsRelative();
    }

    private void _okButton_Click(object sender, RoutedEventArgs e)
    {
      var testSuiteName = _testSuiteName.Text;

      if (string.IsNullOrWhiteSpace(testSuiteName))
      {
        MessageBox.Show(this, "Name of test suite can't be empty.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _testSuiteName.Focus();
        return;
      }

      var root = Path.GetFullPath(_rootFolder);
      var path = Path.Combine(root, testSuiteName);

      if (Directory.Exists(path) && _create)
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

      MakeAllPathsRelative();

      var assemblyPaths = Utils.GetAssemblyPaths(_assemblies.Text);

      if (assemblyPaths.Length == 0)
      {
        MessageBox.Show(this, "No one valid library in library list.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _assemblies.Focus();
        return;
      }

      var syntaxModules = GetSelectedGrammarDescriptor();

      if (syntaxModules.Length == 0)
      {
        MessageBox.Show(this, "No syntax module is selected.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _syntaxModules.Focus();
        return;
      }

      var startRule = _startRuleComboBox.SelectedItem as RuleDescriptor;

      if (startRule == null)
      {
        MessageBox.Show(this, "No a start rule is selected.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _syntaxModules.Focus();
        return;
      }

      try
      {
        if (_baseTestSuite != null && _baseTestSuite.Name != testSuiteName && Directory.Exists(_baseTestSuite.FullPath))
        {
          Directory.CreateDirectory(path);
          FileSystem.CopyDirectory(_baseTestSuite.FullPath, path, UIOption.AllDialogs);
          Directory.Delete(_baseTestSuite.FullPath, recursive: true);
        }
        else
          Directory.CreateDirectory(path);

        var xml = Utils.MakeXml(root, syntaxModules, startRule, _languageName.Text);
        var configPath = Path.Combine(path, "config.xml");
        xml.Save(configPath);
        TestSuiteName = testSuiteName;
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
        InitialDirectory = _rootFolder,
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
