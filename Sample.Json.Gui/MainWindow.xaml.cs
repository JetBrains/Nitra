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
    bool _doTreeOperation;

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
      textBox1.TextArea.Caret.PositionChanged += (o, ea) =>
      {
        ShowInfo(textBox1.CaretOffset);
      };
    }

    private void Parse()
    {
      if (_doTreeOperation)
        return;

      if (_parserHost == null)
        return;

      var source = new SourceSnapshot(textBox1.Text);
      _parseResult = _parserHost.DoParsing(source, JsonParser.GrammarImpl.StartRuleDescriptor);
    }

    void ShowInfo(int pos)
    {
      if (_doTreeOperation)
        return;

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
        node.Header = ruleApplication;
        //node.IsExpanded = true;
        items.Add(node);

        //Fill(node.Items, ruleApplication.Subrules);
      }
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
            'GlossSee': 'markup',
            'A': 42,
            'B': null,
            'B': nullx,
            'B': true,
            'B': false
                  }
              }
          }
      }
}";

    private void textBox1_TextChanged(object sender, EventArgs e)
    {
      Parse();
      ShowInfo(textBox1.CaretOffset);
    }

    private void textBox1_LostFocus(object sender, RoutedEventArgs e)
    {
      textBox1.TextArea.Caret.Show();
    }

    private void treeView1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
      _doTreeOperation = true;
      try
      {
        var caretOffset = textBox1.CaretOffset;
        var item = (TreeViewItem)e.NewValue;

        if (item == null)
          return;

        var info = (RuleApplication)item.Header;
        var size = info.Structure.CalcSize(_parseResult, info.AstPointer);

        textBox1.TextArea.AllowCaretOutsideSelection();
        textBox1.SelectionStart = caretOffset;
        textBox1.SelectionLength = size;
        textBox1.CaretOffset = caretOffset;
      }
      finally
      {
        _doTreeOperation = false;
      }
    }
  }
}
