using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.Win32;
using N2.Runtime.Reflection;
using N2.Visualizer.Properties;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.AddIn;
using ICSharpCode.SharpDevelop.Editor;
using System.Windows.Input;
using N2.Internal;

namespace N2.Visualizer
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    ParserHost _parserHost;
    Parser _parseResult;
    RuleDescriptor _ruleDescriptor;
    bool _doTreeOperation;
    bool _doChangeCaretPos;
    Timer _parseTimer;
    Dictionary<string, HighlightingColor> _highlightingStyles;
    TextMarkerService _textMarkerService; 
    N2FoldingStrategy _foldingStrategy;
    FoldingManager _foldingManager;
    ToolTip _textBox1Tooltip;

    public MainWindow()
    {
      InitializeComponent();
      _foldingStrategy = new N2FoldingStrategy();
      _textBox1Tooltip = new ToolTip() { PlacementTarget = textBox1 };
      _parseTimer = new Timer { AutoReset = false, Enabled = false, Interval = 300 };
      _parseTimer.Elapsed += new ElapsedEventHandler(_parseTimer_Elapsed);

      textBox1.TextArea.Caret.PositionChanged += new EventHandler(Caret_PositionChanged);

      _highlightingStyles = new Dictionary<string, HighlightingColor>
      {
        { "Keyword",  new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Blue) } },
        { "Comment",  new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Green) } },
        { "Number",   new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Magenta) } },
        { "Operator", new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Navy) } },
        { "String",   new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Maroon) } },
      };

      _foldingManager = FoldingManager.Install(textBox1.TextArea);
      _textMarkerService = new TextMarkerService(textBox1.Document);
      textBox1.TextArea.TextView.BackgroundRenderers.Add(_textMarkerService);
      textBox1.TextArea.TextView.LineTransformers.Add(_textMarkerService);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      var args = Environment.GetCommandLineArgs();
      textBox1.Text =
        args.Length > 1
          ? File.ReadAllText(args[1])
          : Settings.Default.LastTextInput;
      if (!string.IsNullOrEmpty(Settings.Default.LastAssemblyFilePath) && File.Exists(Settings.Default.LastAssemblyFilePath))
      {
        var grammars = LoadAssembly(Settings.Default.LastAssemblyFilePath);

        GrammarDescriptor grammar = null;
        if (!string.IsNullOrEmpty(Settings.Default.LastGrammarName))
          grammar = grammars.FirstOrDefault(g => g.FullName == Settings.Default.LastGrammarName);

        RuleDescriptor ruleDescriptor = null;
        if (grammar != null)
          ruleDescriptor = grammar.Rules.FirstOrDefault(r => r.Name == Settings.Default.LastRuleName);

        if (ruleDescriptor != null)
          LoadRule(ruleDescriptor);
        else
        {
          var dialog = new RuleSelectionDialog(grammars);
          if (dialog.ShowDialog() ?? false)
            LoadRule(dialog.Result);
        }
      }

      _parseTimer.Start();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
      Settings.Default.LastTextInput = textBox1.Text;
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

    private void TryReportError()
    {
      _textMarkerService.RemoveAll(_ => true);

      if (_parseResult.IsSuccess)
      {
        _status.Text = "OK";
        return;
      }

      var errPos = _parseResult.LastSuccessPos;

      var marker = _textMarkerService.Create(errPos, 1);
      marker.MarkerType = TextMarkerType.SquigglyUnderline;
      marker.MarkerColor = Colors.Red;
      marker.ToolTip = "Parse error";

      var set = new HashSet<string>();
      for (int i = errPos; i >= 0; i--)
      {
        var applications = _parseResult.ParserHost.Reflection(_parseResult, i);
        foreach (var a in applications)
        {
          var failed = a.FirstFailedIndex;
          if (failed >= 0)
          {
            var sate = _parseResult.ast[a.AstPointer + 2];
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

        var root = _parseResult.Reflect();
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
        //  var sate = _parseResult.ast[ruleApplication.AstPointer + 2];
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

    private void textBox1_TextChanged(object sender, EventArgs e)
    {
      _parseTimer.Stop();
      _textBox1Tooltip.IsOpen = false;
      _parseTimer.Start();
    }

    private void textBox1_LostFocus(object sender, RoutedEventArgs e)
    {
      textBox1.TextArea.Caret.Show();
    }

    private static string MakePath(TreeViewItem item)
    {
      var path = new Stack<string>();

      for (var curr = item; curr != null; curr = curr.Parent as TreeViewItem)
        path.Push(curr.Header.ToString());

      return path.Count == 0 ? "" : string.Join(@"\", path);
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
      var dialog = new OpenFileDialog
      {
        DefaultExt = ".dll",
        Filter = "Parser module (.dll)|*.dll"
      };
      if (!string.IsNullOrEmpty(Settings.Default.LastLoadParserDirectory) && Directory.Exists(Settings.Default.LastLoadParserDirectory))
        dialog.InitialDirectory = Settings.Default.LastLoadParserDirectory;

      if (dialog.ShowDialog(this) ?? false)
      {
        Settings.Default.LastLoadParserDirectory = Path.GetDirectoryName(dialog.FileName);

        var grammars = LoadAssembly(dialog.FileName);
        var ruleSelectionDialog = new RuleSelectionDialog(grammars) { Owner = this };
        if (ruleSelectionDialog.ShowDialog() ?? false)
        {
          LoadRule(ruleSelectionDialog.Result);
        }
      }
    }

    private GrammarDescriptor[] LoadAssembly(string assemblyFilePath)
    {
      var assembly = Assembly.LoadFrom(assemblyFilePath);
      Settings.Default.LastAssemblyFilePath = assemblyFilePath;
      return GrammarDescriptor.GetDescriptors(assembly);
    }

    private void LoadRule(RuleDescriptor ruleDescriptor)
    {
      Settings.Default.LastGrammarName = ruleDescriptor.Grammar.FullName;
      Settings.Default.LastRuleName = ruleDescriptor.Name;

      _ruleDescriptor = ruleDescriptor;
      _parserHost = new ParserHost();
      _parserHost.RecoveryStrategy = new Recovery().Strategy;
      _parseResult = null;

      treeView1.Items.Clear();
    }

    private void FileOpen_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new OpenFileDialog { Filter = "C# (.cs)|*.cs|Text (.txt)|*.txt|All|*.*" };
      if (dialog.ShowDialog(this) ?? false)
      {
        textBox1.Text = File.ReadAllText(dialog.FileName);
      }
    }

    void _parseTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
      Dispatcher.Invoke(new Action(DoParse));
    }

    private void DoParse()
    {
      if (_doTreeOperation)
        return;

      if (_parserHost == null)
        return;

      try
      {
        var source = new SourceSnapshot(textBox1.Text);

        var simpleRule = _ruleDescriptor as SimpleRuleDescriptor;

        if (simpleRule != null)
          _parseResult = _parserHost.DoParsing(source, simpleRule);
        else
          _parseResult = _parserHost.DoParsing(source, (ExtensibleRuleDescriptor)_ruleDescriptor);

        textBox1.TextArea.TextView.Redraw(DispatcherPriority.Input);

        _foldingStrategy.Parser = _parseResult;
        _foldingStrategy.UpdateFoldings(_foldingManager, textBox1.Document);

        TryReportError();
        ShowInfo();
      }
      catch (TypeLoadException ex)
      {
        MessageBox.Show(this, ex.Message);
      }
      catch (ErrorException ex)
      {
        _parseResult = null;
        var recovery = ex.Recovery;
        if (recovery == null)
          return;

        _textMarkerService.RemoveAll(_ => true);

        var marker = _textMarkerService.Create(recovery.FailPos, recovery.SkipedCount);// == 0 ? 1 : recovery.SkipedCount);
        marker.MarkerType = TextMarkerType.SquigglyUnderline;
        marker.MarkerColor = Colors.Red;
        marker.ToolTip = "Parse error: State= " + recovery.StartState + "\r\n  " + string.Join("\r\n    ", recovery.Stack.Select(s => s.ToString()));
        _status.Text = "Parse error: State= " + recovery.StartState + "     " + recovery.Stack.Head;
        ShowInfo();
      }
    }

    private void textBox1_HighlightLine(object sender, HighlightLineEventArgs e)
    {
      if (_parseResult == null)
        return;

      var line = e.Line;
      var spans = new List<SpanInfo>();
      _parseResult.GetSpans(line.Offset, line.EndOffset, spans);
      foreach (var span in spans)
      {
        HighlightingColor color;
        if (_highlightingStyles.TryGetValue(span.SpanClass.Name, out color))
        {
          var startOffset = Math.Max(line.Offset, span.Location.StartPos);
          var endOffset = Math.Min(line.EndOffset, span.Location.EndPos);
          var section = new HighlightedSection
          {
            Offset = startOffset,
            Length = endOffset - startOffset,
            Color = color
          };
          e.Sections.Add(section);
        }
      }
    }

    private void textBox1_MouseHover(object sender, MouseEventArgs e)
    {
      var pos = textBox1.TextArea.TextView.GetPositionFloor(e.GetPosition(textBox1.TextArea.TextView) + textBox1.TextArea.TextView.ScrollOffset);
      if (pos.HasValue)
      {
        var offset = textBox1.Document.GetOffset(new TextLocation(pos.Value.Line, pos.Value.Column));
        var markersAtOffset = _textMarkerService.GetMarkersAtOffset(offset);
        var markerWithToolTip = markersAtOffset.FirstOrDefault(marker => marker.ToolTip != null);
        if (markerWithToolTip != null)
        {
          _textBox1Tooltip.Content = markerWithToolTip.ToolTip;
          _textBox1Tooltip.IsOpen = true;
        }
      }
    }

    private void textBox1_MouseHoverStopped(object sender, MouseEventArgs e)
    {
      _textBox1Tooltip.IsOpen = false;
    }
  }
}
