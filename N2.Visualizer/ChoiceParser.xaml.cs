using N2;

using System;
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
using System.Windows.Shapes;
using System.Reflection;
using System.Diagnostics;
using System.ComponentModel;

namespace N2.Visualizer
{
  /// <summary>
  /// Interaction logic for ChoiceParser.xaml
  /// </summary>
  public partial class ChoiceParser : Window
  {
    public RuleDescriptor Result { get; private set; }

    public ChoiceParser(IEnumerable<GrammarDescriptor> grammars)
    {
      InitializeComponent();
      foreach (var grammar in grammars)
      {
        var item = new ListBoxItem
        {
          Tag = grammar,
          Content = MakeDisplayName(grammar),
        };
        _parsersListBox.Items.Add(item);
      }

      if (_parsersListBox.Items.Count > 0)
        _parsersListBox.SelectedItem = _parsersListBox.Items[0];
    }

    private void button1_Click(object sender, RoutedEventArgs e)
    {
      TryClose();
    }

    private void _ruleListListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      TryClose();
    }

    private void TryClose()
    {
      ListBox ruleList;
      if (tabControl1.SelectedItem == _startRulesTab)
        ruleList = _startRulesListBox;
      else if (tabControl1.SelectedItem == _allRulesTab)
        ruleList = _allRulesListBox;
      else
        ruleList = null;

      if (ruleList != null && ruleList.SelectedItem != null)
      {
        Result = (RuleDescriptor)((ListBoxItem)ruleList.SelectedItem).Tag;
        DialogResult = true;
        Close();
      }
    }

    private void _parsersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      _startRulesListBox.Items.Clear();
      _allRulesListBox.Items.Clear();

      var grammar = (GrammarDescriptor)((ListBoxItem)_parsersListBox.SelectedItem).Tag;
      foreach (var rule in grammar.Rules)
      {
        if (rule is SimpleRuleDescriptor || rule is ExtensibleRuleDescriptor)
        {
          var node = new ListBoxItem { Content = rule.Name, Tag = rule };
          _allRulesListBox.Items.Add(node);
        }
        if (rule.IsStartRule)
        {
          var node = new ListBoxItem { Content = rule.Name, Tag = rule };
          _startRulesListBox.Items.Add(node);
        }
      }

      _allRulesListBox.Items.SortDescriptions.Add(new SortDescription("Content", ListSortDirection.Ascending));

      if (_startRulesListBox.Items.Count > 0)
        _startRulesListBox.SelectedItem = _startRulesListBox.Items[0];

      if (_allRulesListBox.Items.Count > 0)
        _allRulesListBox.SelectedItem = _allRulesListBox.Items[0];
    }

    private static string MakeDisplayName(GrammarDescriptor grammar)
    {
      return string.IsNullOrEmpty(grammar.Namespace)
        ? grammar.Name
        : grammar.Name + "(" + grammar.Namespace + ")";
    }
  }
}
