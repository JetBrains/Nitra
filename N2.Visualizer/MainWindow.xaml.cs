using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using Microsoft.Win32;

namespace N2.Visualizer
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    ParserHost      _parserHost;
    ParseResult     _parseResult;
    RuleDescriptor  _ruleDescriptor = JsonParser.GrammarImpl.StartRuleDescriptor;
    bool            _doTreeOperation;

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

      var simpleRule = _ruleDescriptor as SimpleRuleDescriptor;

      if (simpleRule != null)
        _parseResult = _parserHost.DoParsing(source, simpleRule);
      else
        _parseResult = _parserHost.DoParsing(source, (ExtensibleRuleDescriptor)_ruleDescriptor);

      if (!_parseResult.IsSuccess)
      {
        var i = _parseResult.LastSuccessPos;
        textBox1.CaretOffset = i;
        textBox1.SelectionStart = i;
        textBox1.SelectionLength = 1;
      }
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

        var size = ruleApplication.Size;

        if (size == 0)
          node.Background = new SolidColorBrush(Color.FromRgb(200, 255, 200));

        if (ruleApplication.FirstFailedIndex > 0)
        {
          var sate = _parseResult.RawAst[ruleApplication.AstPointer + 2];

          if (sate >= 0)
            node.Background = new SolidColorBrush(Color.FromRgb(255, 200, 200));
        }

        node.Expanded += new RoutedEventHandler(node_Expanded);

        if (ruleApplication.GetChildren().Any())
          node.Items.Add(new TreeViewItem());

        items.Add(node);
      }
    }

    void node_Expanded(object sender, RoutedEventArgs e)
    {
      var node = (TreeViewItem)e.Source;
      if (node.Items.Count == 1 && ((TreeViewItem)node.Items[0]).Header == null)
      {
        node.Items.Clear();

        if (node.Header is RuleApplication)
        {
          var ruleApplication = (RuleApplication)node.Header;
          var calls = ruleApplication.GetChildren();
          var failed = ruleApplication.FirstFailedIndex;
          var i = 0;

          foreach (var call in calls)
          {
            var subNode = new TreeViewItem();
            subNode.Tag = call;
            subNode.Header = call;
            var hasChildren = call.GetChildren().Any();
            if (hasChildren)
              subNode.Items.Add(new TreeViewItem());
            subNode.BorderThickness = new Thickness(1);
            subNode.BorderBrush = new SolidColorBrush(Colors.Brown);

            if (failed >= 0 && i >= failed)
              subNode.Background = new SolidColorBrush(Color.FromRgb(255, 200, 200));

            node.Items.Add(subNode);
            i++;
          }
        }
        else if (node.Header is RuleCall)
        {
          var call = (RuleCall)node.Header;
          Fill(node.Items, call.GetChildren());
        }
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
            'C': nullx,
            'D': xnull,
            'E': true,
            'F': false
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
        var item = (TreeViewItem)e.NewValue;

        if (item == null)
          return;

        var info = (IRuleApplication)item.Header;

        textBox1.TextArea.AllowCaretOutsideSelection();
        textBox1.Select(info.Position, info.Size);
      }
      finally
      {
        _doTreeOperation = false;
      }
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
      this.Close();
    }

    private void ParserLoad_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new OpenFileDialog();
      dialog.DefaultExt = ".dll";
      dialog.Filter = "Parser module (.dll)|*.dll";

      if (dialog.ShowDialog(this) ?? false)
      {
        var asm = Assembly.LoadFrom(dialog.FileName);
        var grammarAttrs = asm.GetCustomAttributes(typeof(GrammarsAttribute), false).OfType<GrammarsAttribute>();
        var grammarTypes = new List<Type>();

        foreach (var attr in grammarAttrs)
          grammarTypes.AddRange(attr.Grammars);

        var choiceParser = new ChoiceParser(grammarTypes.ToArray());
        choiceParser.Owner = this;
        if (choiceParser.ShowDialog() ?? false)
        {
          _ruleDescriptor = choiceParser.Result;
          _parserHost     = new ParserHost();
          _parseResult    = null;

          treeView1.Items.Clear();
        }
      }
    }

    private void FileOpen_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new OpenFileDialog();
      dialog.Filter = "C# (.cs)|*.cs|Text (.txt)|*.txt|All|*.*";

      if (dialog.ShowDialog(this) ?? false)
      {
        textBox1.Text = File.ReadAllText(dialog.FileName);
      }
    }
  }
}
