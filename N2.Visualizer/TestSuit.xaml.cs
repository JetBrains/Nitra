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
using N2.Visualizer.Properties;
using System.Collections.ObjectModel;

namespace N2.Visualizer
{
  /// <summary>
  /// Interaction logic for Test.xaml
  /// </summary>
  public partial class TestSuit : Window
  {
    const string _showAllRules       = "<Show all rules>";
    const string _showOnlyStratRules = "<Show only strat rules>";
    Settings _settings;

    public TestSuit()
    {
      _settings = Settings.Default;

      InitializeComponent();
      var testsLocationRootFullPath = Path.GetFullPath(_settings.TestsLocationRoot);
      _testsRootTextBlock.Text = testsLocationRootFullPath;
      var relativeAssemblyPath = Utils.MakeMakeRelativePath(testsLocationRootFullPath, _settings.LastAssemblyFilePath);
      _assemblies.Text = relativeAssemblyPath;
      UpdateSyntaxModules(relativeAssemblyPath, testsLocationRootFullPath);
    }

    private void UpdateSyntaxModules(string relativeAssemblyPaths, string testsLocationRootFullPath)
    {
      var syntaxModules = new List<SyntaxModuleVm>();

      foreach (var relativeAssemblyPath in relativeAssemblyPaths.Split(new []{ "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
      {
        var path = Path.Combine(testsLocationRootFullPath, relativeAssemblyPath);
        var fullPath = Path.GetFullPath(path);
        var grammarDescriptors = Utils.LoadAssembly(fullPath);
        foreach (var grammarDescriptor in grammarDescriptors)
          syntaxModules.Add(new SyntaxModuleVm(grammarDescriptor));
      }

      _syntaxModules.ItemsSource = new ObservableCollection<SyntaxModuleVm>(SortSyntaxModules(syntaxModules));

      UpdateStartRules(true);
    }

    private static IEnumerable<SyntaxModuleVm> SortSyntaxModules(List<SyntaxModuleVm> syntaxModules)
    {
      return syntaxModules.OrderBy(m => m.Name);
    }

    private void UpdateStartRules(bool showOnlyStratRules)
    {
      string name = null;
      var oldSelected = _startRuleComboBox.SelectedItem;

      var rules = new List<object>();
      var syntaxModules = (ObservableCollection<SyntaxModuleVm>)_syntaxModules.ItemsSource;
      foreach (var syntaxModule in syntaxModules)
        if (syntaxModule.IsChecked)
        {
          if (name == null)
            name = syntaxModule.Name;
          else
            name += "-" + syntaxModule.Name;

          foreach (var rule in GetRules(syntaxModule))
            if (rule.IsStartRule || !showOnlyStratRules)
              rules.Add(rule);
        }

      _startRuleComboBox.IsEnabled = rules.Count > 0;

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

      if (name != null)
        _testSuitName.Text = name;

      _startRuleComboBox.ItemsSource = rules;
    }

    private static IOrderedEnumerable<RuleDescriptor> GetRules(SyntaxModuleVm syntaxModule)
    {
      return syntaxModule.GrammarDescriptor.Rules.OrderBy(r => r.ToString());
    }

    private void _okButton_Click(object sender, RoutedEventArgs e)
    {
      var path = Path.Combine(_settings.TestsLocationRoot, _settings.LastGrammarName, _settings.LastRuleName);
      //var filePath = Path.Combine(path, _testSuitName.Text);

      //try 
      //{	        
      //  File.WriteAllText(filePath, _code);
      //}
      //catch (Exception ex)
      //{
      //  MessageBox.Show(this, ex.Message, "Nitra Visualizer",
      //    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.Cancel);
      //  return;
      //}

      //var goldFilePath = Path.ChangeExtension(filePath, ".gold");

      //File.WriteAllText(goldFilePath, _gold);

      //TestName = _testName.Text;
      this.DialogResult = true;
      Close();
    }

    private void CheckBox_Changed(object sender, RoutedEventArgs e)
    {
      UpdateStartRules(true);
    }

    private void _startRuleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (_startRuleComboBox.SelectedItem == (object)_showAllRules)
        UpdateStartRules(false);
      else if (_startRuleComboBox.SelectedItem == (object)_showOnlyStratRules)
        UpdateStartRules(true);
    }
  }
}
