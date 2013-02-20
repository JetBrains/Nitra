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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using N2;
using N2.Tests;
using N2.Runtime.Reflection;
using System.Collections.ObjectModel;

namespace Sample.Json.Gui
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    ParserHost _parserHost;
    ParseResult _parseResult;

    public MainWindow()
    {
      InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      var args = Environment.GetCommandLineArgs();
      if (args.Length > 1)
        text = File.ReadAllText(args[1]);

      textBox1.Text = text;

      _parserHost = new ParserHost();
      Parse();
    }

    private void Parse()
    {
      if (_parserHost == null)
        return;

      var source = new SourceSnapshot(textBox1.Text);
      _parseResult = _parserHost.DoParsing(source, JsonParser.GrammarImpl.StartRuleDescriptor);
    }

    void ShowInfo(int pos)
    {
      try
      {
        if (_parseResult == null)
          return;

        treeView1.Items.Clear();

        if (pos > _parseResult.RawMemoize.Length)
          return;

        Fill(treeView1.Items, _parseResult.ParserHost.Reflection(_parseResult, pos));

        //_lbRules.Items.AddRange(ParseResult.ParserHost.Reflection(ParseResult, pos));
      }
      finally
      {
        //_lbRules.EndUpdate();
      }
    }

    private void Fill(ItemCollection items, ReadOnlyCollection<RuleApplication> ruleApplications)
    {
      foreach (RuleApplication ruleApplication in ruleApplications)
      {
        var node = new TreeViewItem();
        node.Header = ruleApplication.ToString();
        //node.IsExpanded = true;
        items.Add(node);

        Fill(node.Items, ruleApplication.Subrules);
      }
    }

    private void textBox1_SelectionChanged(object sender, RoutedEventArgs e)
    {
      ShowInfo(textBox1.CaretIndex);
    }

    static string text =
  @"{
      'glossary': {
          'title': 'example glossary',
      'GlossDiv': {
              'title': 'S',
        'GlossList': {
                  'GlossEntry': {
                      'ID': 'SGML',
            'SortAs': 'SGML',
            'GlossTerm': 'Standard Generalized Markup Language',
            'Acronym': 'SGML',
            'Abbrev': 'ISO 8879:1986',
            'GlossDef': {
                          'para': 'A meta-markup language, used to create markup languages such as DocBook.',
              'GlossSeeAlso': ['GML', 'XML']
                      },
            'GlossSee': 'markup'
                  }
              }
          }
      }
}";

    private void textBox1_TextChanged(object sender, TextChangedEventArgs e)
    {
      Parse();
      ShowInfo(textBox1.CaretIndex);
    }
  }
}
