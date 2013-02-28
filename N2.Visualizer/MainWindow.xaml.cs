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
using System.Diagnostics;
using ICSharpCode.AvalonEdit.Rendering;

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
    bool            _doChangeCaretPos;
    HighlightErrorBackgroundRendere _errorHighlighter;

    public MainWindow()
    {
      InitializeComponent();
      //textBox1.TextArea.TextView.LineTransformers.Add();// Services.AddService(typeof(ITextMarkerService), textMarkerService);
      //textBox1.TextArea.TextView.Services.AddService(typeof(ITextMarkerService), textMarkerService);
    
      _errorHighlighter = new HighlightErrorBackgroundRendere(textBox1);
      textBox1.TextArea.TextView.BackgroundRenderers.Add(_errorHighlighter);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      var args = Environment.GetCommandLineArgs();
      if (args.Length > 1)
        text = File.ReadAllText(args[1]);

      textBox1.Text = text;

      _parserHost = new ParserHost();
      Parse();
      textBox1.TextArea.Caret.PositionChanged += new EventHandler(Caret_PositionChanged);
    }

    private void textBox1_GotFocus(object sender, RoutedEventArgs e)
    {
      ShowNodeForCaret();
    }

    void Caret_PositionChanged(object sender, EventArgs e)
    {
      _pos.Text = textBox1.CaretOffset.ToString();
      ShowNodeForCaret();
    }

    private void ShowNodeForCaret()
    {
      if (_doTreeOperation)
        return;

      _doChangeCaretPos = true;
      try
      {
        var node = FindNode(treeView1.Items, textBox1.CaretOffset);

        if (node != null)
        {
          node.IsSelected = true;
          node.BringIntoView();
        }
      }
      finally
      {
        _doChangeCaretPos = false;
      }
    }

    private TreeViewItem FindNode(ItemCollection items, int p)
    {

      foreach (TreeViewItem item in items)
      {
        item.IsExpanded = true;
        var node = (ReflectionStruct)item.Tag;

        if (node.Location.StartPos <= p && p < node.Location.EndPos)
        {
          item.IsExpanded = true;

          if (item.Items.Count == 0)
            return item;

          return FindNode(item.Items, p);
        }
      }

      return null;
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

      TryReportError();
      ShowInfo();
    }

    private void TryReportError()
    {
      if (_parseResult.IsSuccess)
      {
        _errorHighlighter.ErrorPos = -1;
        _status.Text = "OK";
        return;
      }

      var errPos = _parseResult.LastSuccessPos;
      _errorHighlighter.ErrorPos = errPos;
      var set = new HashSet<string>();
      for (int i = errPos; i >= 0; i--)
      {
        var applications = _parseResult.ParserHost.Reflection(_parseResult, i);
        foreach (var a in applications)
        {
          var failed = a.FirstFailedIndex;
          if (failed >= 0)
          {
            var sate = _parseResult.RawAst[a.AstPointer + 2];
            var calls = a.GetChildren();
            var e = 0;
            var size = 0;

            foreach (var call in calls)
            {
              if (failed >= 0 && e >= failed)
              {
                if (size > 0)
                {
                  _status.Text = "Expected: " + call;
                  return;
                }
              }
              else
                size += call.Size;
              e++;
            }
          }
        }
      }
    }

    void ShowInfo()
    {
      try
      {
        treeView1.Items.Clear();

        if (_parseResult == null || !_parseResult.IsSuccess)
          return;

        var root = _parseResult.Reflection();
        var treeNode = new TreeViewItem();
        treeNode.Expanded += new RoutedEventHandler(node_Expanded);
        treeNode.Header = root.Description;
        treeNode.Tag = root;
        if (root.Children.Count != 0)
          treeNode.Items.Add(new TreeViewItem());
        treeView1.Items.Add(treeNode);
        
      }
      catch { }
    }

    void Fill(ItemCollection treeNodes, ReadOnlyCollection<ReflectionStruct> nodes)
    {
      foreach (var node in nodes)
	    {
        var treeNode = new TreeViewItem();
        treeNode.Header = node.Description;
        treeNode.Tag = node;
        treeNode.Expanded += new RoutedEventHandler(node_Expanded);
        if (node.Location.Length == 0)
          treeNode.Background = new SolidColorBrush(Color.FromRgb(200, 255, 200));
        //if (ruleApplication.FirstFailedIndex > 0)
        //{
        //  var sate = _parseResult.RawAst[ruleApplication.AstPointer + 2];
        //  if (sate >= 0)
        //    node.Background = new SolidColorBrush(Color.FromRgb(255, 200, 200));
        //}

        treeNodes.Add(treeNode);

        if (node.Children.Count != 0)
          treeNode.Items.Add(new TreeViewItem());
	    }
    }

    void node_Expanded(object sender, RoutedEventArgs e)
    {
      var treeNode = (TreeViewItem)e.Source;
      if (treeNode.Items.Count == 1 && ((TreeViewItem)treeNode.Items[0]).Header == null)
      {
        treeNode.Items.Clear();
        var node = (ReflectionStruct)treeNode.Tag;
        Fill(treeNode.Items, node.Children);
        treeNode.Expanded -= new RoutedEventHandler(node_Expanded);
      }
    }

    private static bool isSimpleCall(RuleCall call)
    {
      return call.RuleInfo.Visit<bool>(simpleCall: id => true, noMatch: () => false);
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
    }

    private void textBox1_LostFocus(object sender, RoutedEventArgs e)
    {
      textBox1.TextArea.Caret.Show();
    }

    string MakePath(TreeViewItem item)
    {
      var path = new List<string>();

      for (TreeViewItem curr = item; curr != null; curr = curr.Parent as TreeViewItem)
        path.Add(curr.Header.ToString());

      path.Reverse();

      if (path.Count == 0)
          return "";

      return string.Join(@"\", path);
    }

    private void treeView1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
      var item = (TreeViewItem)e.NewValue;

      if (item == null)
        return;

      if (_parseResult != null && _parseResult.IsSuccess)
        _status.Text = MakePath(item);

      if (_doChangeCaretPos)
        return;

      _doTreeOperation = true;
      try
      {
        var info = (ReflectionStruct)item.Tag;

        textBox1.TextArea.AllowCaretOutsideSelection();
        textBox1.Select(info.Location.StartPos, info.Location.Length);
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
