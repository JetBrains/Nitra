using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.AddIn;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.SharpDevelop.Editor;
using Microsoft.Win32;
using N2.Internal;
using N2.Runtime.Reflection;
using N2.Strategies;
using N2.Visualizer.Properties;

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
      _findGrid.Visibility = System.Windows.Visibility.Collapsed;
      _foldingStrategy = new N2FoldingStrategy();
      _textBox1Tooltip = new ToolTip() { PlacementTarget = _text };
      _parseTimer = new Timer { AutoReset = false, Enabled = false, Interval = 300 };
      _parseTimer.Elapsed += new ElapsedEventHandler(_parseTimer_Elapsed);

      _text.TextArea.Caret.PositionChanged += new EventHandler(Caret_PositionChanged);

      _highlightingStyles = new Dictionary<string, HighlightingColor>
      {
        { "Keyword",  new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Blue) } },
        { "Comment",  new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Green) } },
        { "Number",   new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Magenta) } },
        { "Operator", new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Navy) } },
        { "String",   new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Maroon) } },
      };

      _foldingManager = FoldingManager.Install(_text.TextArea);
      _textMarkerService = new TextMarkerService(_text.Document);
      _text.TextArea.TextView.BackgroundRenderers.Add(_textMarkerService);
      _text.TextArea.TextView.LineTransformers.Add(_textMarkerService);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      var args = Environment.GetCommandLineArgs();
      _text.Text =
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
      Settings.Default.LastTextInput = _text.Text;
    }

    private void textBox1_GotFocus(object sender, RoutedEventArgs e)
    {
      ShowNodeForCaret();
    }

    void Caret_PositionChanged(object sender, EventArgs e)
    {
      _pos.Text = _text.CaretOffset.ToString();
      ShowNodeForCaret();
    }

    private void ShowNodeForCaret()
    {
      if (_doTreeOperation)
        return;

      _doChangeCaretPos = true;
      try
      {
        var node = FindNode(treeView1.Items, _text.CaretOffset);

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
      ClearMarkers();

      if (_parseResult.IsSuccess)
      {
        _status.Text = "OK";
      }
      else
      {
        var errors = _parseResult.GetErrors();
        foreach (ParseError error in errors)
        {
          var location = error.Location;
          var marker = _textMarkerService.Create(location.StartPos, location.Length);
          marker.MarkerType = TextMarkerType.SquigglyUnderline;
          marker.MarkerColor = Colors.Red;
          marker.ToolTip = error.Message + "\r\n\r\n" + error.DebugText;
        }
        _status.Text = "Parsing completed with errors";
      }
    }

    void ShowInfo()
    {
      try
      {
        treeView1.Items.Clear();

        if (_parseResult == null)
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
      _text.TextArea.Caret.Show();
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

        _text.TextArea.AllowCaretOutsideSelection();
        _text.Select(info.Location.StartPos, info.Location.Length);
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

    private void ParserLoad(object sender, RoutedEventArgs e)
    {
      var dialog = new OpenFileDialog
      {
        DefaultExt = ".dll",
        Filter = "Parser module (.dll)|*.dll",
        Title = "Load partser"
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

    private void FileOpenExecuted(object sender, RoutedEventArgs e)
    {
      var dialog = new OpenFileDialog { Filter = "C# (.cs)|*.cs|Text (.txt)|*.txt|All|*.*" };
      if (dialog.ShowDialog(this) ?? false)
      {
        _text.Text = File.ReadAllText(dialog.FileName);
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
        var source = new SourceSnapshot(_text.Text);

        var simpleRule = _ruleDescriptor as SimpleRuleDescriptor;

        if (simpleRule != null)
          _parseResult = _parserHost.DoParsing(source, simpleRule);
        else
          _parseResult = _parserHost.DoParsing(source, (ExtensibleRuleDescriptor)_ruleDescriptor);

        _text.TextArea.TextView.Redraw(DispatcherPriority.Input);

        _foldingStrategy.Parser = _parseResult;
        _foldingStrategy.UpdateFoldings(_foldingManager, _text.Document);

        TryReportError();
        ShowInfo();
      }
      catch (TypeLoadException ex)
      {
        ClearMarkers();
        MessageBox.Show(this, ex.Message);
      }
    }

    private void ClearMarkers()
    {
      _textMarkerService.RemoveAll(_ => true);
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
      var pos = _text.TextArea.TextView.GetPositionFloor(e.GetPosition(_text.TextArea.TextView) + _text.TextArea.TextView.ScrollOffset);
      if (pos.HasValue)
      {
        var offset = _text.Document.GetOffset(new TextLocation(pos.Value.Line, pos.Value.Column));
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

    private void FindCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = true;
      e.Handled = true;
    }

    private void FindExecuted(object sender, ExecutedRoutedEventArgs e)
    {
      _findGrid.Visibility = System.Windows.Visibility.Visible;
      _findText.Focus();
      e.Handled = true;
    }

    private void _findClose_Click(object sender, RoutedEventArgs e)
    {
      _findGrid.Visibility = System.Windows.Visibility.Collapsed;
    }

    private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = true;
      e.Handled = true;
    }

    private void FindNext_Executed(object sender, ExecutedRoutedEventArgs e)
    {
      FindNext();
    }

    private void FindPrev_Executed(object sender, ExecutedRoutedEventArgs e)
    {
      FindPrev();
    }

    private bool FindNext()
    {
      var option = GetMatchCaseOption();
      var toFind = _findText.Text;
      var text = _text.Text;
      var index = _text.SelectionStart + _text.SelectionLength - 1;

      do
      {
        if (index < text.Length)
          index++;
        index = text.IndexOf(toFind, index, option);
      }
      while (index >= 0 && !IsWholeWord(text, index, toFind.Length));

      var found = index >= 0;
      if (found)
        _text.Select(index, toFind.Length);
      else
        _status.Text = "Can't find '" + toFind + "'.";

      return found;
    }

    private bool FindPrev()
    {
      var option = GetMatchCaseOption();
      var toFind = _findText.Text;
      var text = _text.Text;
      var index = _text.SelectionStart;

      do
      {
        if (index > 0)
          index--;
        index = text.LastIndexOf(toFind, index, option);
      }
      while (index >= 0 && !IsWholeWord(text, index, toFind.Length));

      var found = index >= 0;

      if (found)
        _text.Select(index, toFind.Length);
      else
        _status.Text = "Can't find '" + toFind + "'.";

      return found;
    }

    private bool IsWholeWord(string text, int start, int length)
    {
      if (!(_findMatchWholeWord.IsChecked ?? false))
        return true;

      var end = start + length - 1;
      
      if (end < text.Length)
        if (char.IsLetterOrDigit(text[end]) || text[end] == '_')
          if (char.IsLetterOrDigit(text[end + 1]) || text[end + 1] == '_')
            return false;

      if (start > 0)
        if (char.IsLetterOrDigit(text[start]) || text[start] == '_')
          if (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_')
            return false;

      return true;
    }

    private StringComparison GetMatchCaseOption()
    {
      return _findMatchCase.IsChecked ?? false ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;
    }
  }
}
