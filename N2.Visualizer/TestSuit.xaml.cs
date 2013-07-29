using N2.Visualizer.Properties;

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
using System.Xml.Linq;
using N2.Visualizer.ViewModels;

namespace N2.Visualizer
{
  public partial class TestSuit
  {
    const string _showAllRules       = "<Show all rules>";
    const string _showOnlyStratRules = "<Show only strat rules>";
    const string _assembiesToolTip   = "Enter paths to assemblies (one assembly per line)";

    public string              TestSuitName    { get; private set; }
    public string[]            ParserLibraries { get; private set; }
    public GrammarDescriptor[] SyntaxModules   { get; private set; }
    public RuleDescriptor      StartRule       { get; private set; }

    readonly Settings _settings;
    bool _nameUpdate;
    bool _nameChangedByUser;
    readonly bool _create;

    readonly DispatcherTimer _timer = new DispatcherTimer();

    public TestSuit(bool create)
    {
      _settings = Settings.Default;
      _create   = create;

      InitializeComponent();

      this.Title = create ? "New test suit" : "Edit test suit";

      var testsLocationRootFullPath = Path.GetFullPath(_settings.TestsLocationRoot);
      _testsRootTextBlock.Text = testsLocationRootFullPath;
      var relativeAssemblyPath = Utils.MakeRelativePath(testsLocationRootFullPath, _settings.LastAssemblyFilePath);
      _assemblies.Text = relativeAssemblyPath;
      UpdateSyntaxModules(relativeAssemblyPath, testsLocationRootFullPath);

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
          var grammarDescriptors = Utils.LoadAssembly(fullPath);
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
        _assemblies.ToolTip = "Error: " + ex.Message + Environment.NewLine + _assembiesToolTip;
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
          if (index >= 0)
            _startRuleComboBox.SelectedItem = rules[index];
          else
            _startRuleComboBox.SelectedItem = rules[0];
        }

        if (showOnlyStratRules)
          rules.Insert(0, _showAllRules);
        else
          rules.Insert(0, _showOnlyStratRules);
      }
      else if (showOnlyStratRules)
        rules.Insert(0, _showAllRules);

      _startRuleComboBox.ItemsSource = rules;

      if (string.IsNullOrWhiteSpace(_testSuitName.Text) || !_nameChangedByUser)
        UpdateName();
    }

    private void UpdateName()
    {
      _nameChangedByUser = false;
      _nameUpdate = true;
      try
      {
        _testSuitName.Text = MakeName();
      }
      finally
      {
        _nameUpdate = false;
      }
    }

    private static IOrderedEnumerable<RuleDescriptor> GetRules(SyntaxModuleVm syntaxModule)
    {
      return syntaxModule.GrammarDescriptor.Rules
        .Where(r => r is ExtensibleRuleDescriptor || r is SimpleRuleDescriptor)
        .OrderBy(r => r.ToString());
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

    private void _startRuleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
// ReSharper disable RedundantCast
      if (_startRuleComboBox.SelectedItem == (object)_showAllRules)
        UpdateStartRules(false);
      else if (_startRuleComboBox.SelectedItem == (object)_showOnlyStratRules)
        UpdateStartRules(true);
// ReSharper restore RedundantCast

      if (string.IsNullOrWhiteSpace(_testSuitName.Text) || !_nameChangedByUser)
        UpdateName();
    }

    private void _testSuitName_TextChanged(object sender, TextChangedEventArgs e)
    {
      if (_nameUpdate)
        return;
      _nameChangedByUser = true;
    }

    private void _testSuitName_KeyUp(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.F5)
        UpdateName();
    }

    private void _testSuitName_LostFocus(object sender, RoutedEventArgs e)
    {
      if (string.IsNullOrWhiteSpace(_testSuitName.Text))
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
      UpdateSyntaxModules(_assemblies.Text, Path.GetFullPath(_settings.TestsLocationRoot));
    }

    private void MakeAllPathsRelative()
    {
      var assemblyPaths = _assemblies.Text.Trim('\n', '\r', '\t', ' ');
      var result = new List<string>();
      var testsLocationRootFullPath = Path.GetFullPath(_settings.TestsLocationRoot);
      foreach (var assemblyPath in Utils.GetAssemblyPaths(assemblyPaths))
      {
        var path = Path.Combine(testsLocationRootFullPath, assemblyPath.Trim());
        var fullPath = Path.GetFullPath(path);
        var relativeAssemblyPath = Utils.MakeRelativePath(testsLocationRootFullPath, fullPath);
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
      var testSuitName = _testSuitName.Text;

      if (string.IsNullOrWhiteSpace(testSuitName))
      {
        MessageBox.Show(this, "Name of test suit can't be empty.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _testSuitName.Focus();
        return;
      }

      var root = Path.GetFullPath(_settings.TestsLocationRoot);
      var path = Path.Combine(root, testSuitName);

      if (Directory.Exists(path) && _create)
      {
        MessageBox.Show(this, "The test suit '" + testSuitName + "' already exists.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _testSuitName.Focus();
        return;
      }

      if (Utils.IsInvalidDirName(testSuitName))
      {
        MessageBox.Show(this, "Name of test suit is invalid.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _testSuitName.Focus();
        return;
      }

      MakeAllPathsRelative();

      TestSuitName = testSuitName;
      var assemblyPaths = Utils.GetAssemblyPaths(_assemblies.Text);

      if (assemblyPaths.Length == 0)
      {
        MessageBox.Show(this, "No one valid library in library list.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _assemblies.Focus();
        return;
      }

      ParserLibraries = assemblyPaths;

      var syntaxModules = GetSelectedGrammarDescriptor();

      if (syntaxModules.Length == 0)
      {
        MessageBox.Show(this, "No syntax module is selected.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _syntaxModules.Focus();
        return;
      }

      SyntaxModules = syntaxModules;

      var startRule = _startRuleComboBox.SelectedItem as RuleDescriptor;

      if (startRule == null)
      {
        MessageBox.Show(this, "No a start rule is selected.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _syntaxModules.Focus();
        return;
      }

      try
      {
        Directory.CreateDirectory(path);
        var xml = MakeXml(root, syntaxModules, startRule);
        var configPath = Path.Combine(path, "config.xml");
        xml.Save(configPath);

      }
      catch (Exception ex)
      {
        MessageBox.Show(this, ex.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

      this.DialogResult = true;
      Close();
    }

    private static XElement MakeXml(string root, GrammarDescriptor[] syntaxModules, RuleDescriptor startRule)
    {
      //  <Config>
      //    <Lib Path="../sss/Json.Grammar.dll"><SyntaxModule Name="JasonParser" /></Lib>
      //    <Lib Path="../sss/JsonEx.dll"><SyntaxModule Name="JsonEx" StartRule="Start" /></Lib>
      //  </Config>
      var libs = syntaxModules.GroupBy(m => Utils.MakeRelativePath(root, m.GetType().Assembly.Location))
        .Select(asm =>
          new XElement("Lib", 
              new XAttribute("Path", asm.Key),
              asm.Select(mod => 
                new XElement("SyntaxModule", 
                  new XAttribute("Name", mod.FullName), 
                  mod.Rules.Contains(startRule) ? new XAttribute("StartRule", startRule.Name) : null))
          ));

      return new XElement("Config", libs);
    }
  }
}
