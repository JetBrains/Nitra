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
using N2;
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

    public ChoiceParser(Type[] grammars)
    {
      InitializeComponent();
      foreach (var grammarType in grammars)
      {
        var item = new ListBoxItem();
        item.Tag = grammarType;
        var baseType = grammarType.BaseType;
        item.Content = baseType.Name + (baseType.Namespace == null ? "" : (" (" + baseType.Namespace + ")"));
        _parsersListBox.Items.Add(item);
      }

      if (_parsersListBox.Items.Count > 0)
        _parsersListBox.SelectedItem = _parsersListBox.Items[0];
    }

    private void button1_Click(object sender, RoutedEventArgs e)
    {
      TryClose();
    }

    private void _startRulesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      TryClose();
    }

    private void TryClose()
    {
      if (_startRulesListBox.SelectedItem == null)
        return;

      var prop = (PropertyInfo)((ListBoxItem)(_startRulesListBox.SelectedItem)).Tag;
      var desc = prop.GetValue(null, null);
      Result = (N2.RuleDescriptor)desc;
      DialogResult = true;
      Close();
    }

    private void _parsersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      _startRulesListBox.Items.Clear();
      _allRulesListBox.Items.Clear();

      var grammarType = (Type)((ListBoxItem)_parsersListBox.SelectedItem).Tag;

      //var startRuleDescriptor = typeof(IStartRuleDescriptor);
      //var props = grammarType.GetProperties(BindingFlags.Public | BindingFlags.Static);
      //
      //{
      //  foreach (var p in props)
      //  {
      //    if (startRuleDescriptor.IsAssignableFrom(p.PropertyType))
      //    {
      //      var item = new ListBoxItem();
      //      item.Tag = p;
      //      const string sufix = "RuleDescriptor";
      //      Trace.Assert(p.Name.EndsWith(sufix));
      //      item.Content = p.Name.Substring(0, p.Name.Length - sufix.Length);
      //      _startRulesListBox.Items.Add(item);
      //    }
      //  }
      //
      //  _startRulesListBox.Items.SortDescriptions.Add(new SortDescription("Content", ListSortDirection.Ascending));
      //}

      var desc = grammarType.GetProperty("StaticDescriptor", BindingFlags.Public | BindingFlags.Static);
      var grammarDescriptor = (GrammarDescriptor)desc.GetValue(null, null);

      {
        foreach (var rule in grammarDescriptor.Rules)
        {
          if (rule is SimpleRuleDescriptor || rule is ExtensibleRuleDescriptor)
          {
            var node = new ListBoxItem();
            node.Content = rule.Name;
            node.Tag = rule;
            _allRulesListBox.Items.Add(node);
          }
        }

        _allRulesListBox.Items.SortDescriptions.Add(new SortDescription("Content", ListSortDirection.Ascending));
      }

      if (_startRulesListBox.Items.Count > 0)
        _startRulesListBox.SelectedItem = _startRulesListBox.Items[0];

      if (_allRulesListBox.Items.Count > 0)
        _allRulesListBox.SelectedItem = _allRulesListBox.Items[0];
    }
  }
}
