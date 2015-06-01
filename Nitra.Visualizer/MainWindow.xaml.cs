using System.Windows.Forms;
using Common;
using ICSharpCode.AvalonEdit.AddIn;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.SharpDevelop.Editor;
using Nemerle.Diff;
using Nitra.Declarations;
using Nitra.Runtime.Reflection;
using Nitra.ViewModels;
using Nitra.Visualizer.Properties;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Nitra.Runtime.Binding;
using Nitra.Runtime.Highlighting;
using CheckBox = System.Windows.Controls.CheckBox;
using Clipboard = System.Windows.Clipboard;
using ContextMenu = System.Windows.Controls.ContextMenu;
using Control = System.Windows.Controls.Control;
using DataFormats = System.Windows.DataFormats;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Label = System.Windows.Controls.Label;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Timer = System.Timers.Timer;
using ToolTip = System.Windows.Controls.ToolTip;

namespace Nitra.Visualizer
{
  using System.Windows.Documents;

  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow
  {
    bool _loading = true;
    IParseResult _parseResult;
    bool _doTreeOperation;
    bool _doChangeCaretPos;
    readonly Timer _parseTimer;
    readonly Dictionary<string, HighlightingColor> _highlightingStyles;
    readonly TextMarkerService _textMarkerService;
    readonly NitraFoldingStrategy _foldingStrategy;
    readonly FoldingManager _foldingManager;
    readonly ToolTip _textBox1Tooltip;
    bool _needUpdateReflection;
    bool _needUpdateHtmlPrettyPrint;
    bool _needUpdateTextPrettyPrint;
    bool _needUpdatePerformance;
    ParseTree _parseTree;
    TimeSpan _highlightingTimeSpan;
    readonly Settings _settings;
    private SolutionVm _solution;
    private TestSuitVm _currentTestSuit;
    private TestFolderVm _currentTestFolder;
    private TestVm _currentTest;
    private readonly PropertyGrid _propertyGrid;
    private readonly MatchBracketsWalker _matchBracketsWalker = new MatchBracketsWalker();
    private List<ITextMarker> _matchedBracketsMarkers = new List<ITextMarker>();
    private List<MatchBracketsWalker.MatchBrackets> _matchedBrackets;
    private const string ErrorMarkerTag = "Error";
    private readonly VisualizerCompilerMessages _cmpilerMessages;

    public MainWindow()
    {
      _settings = Settings.Default;

      ToolTipService.ShowDurationProperty.OverrideMetadata(
        typeof(DependencyObject),
        new FrameworkPropertyMetadata(Int32.MaxValue));

      InitializeComponent();

      this.Top         = _settings.WindowTop;
      this.Left        = _settings.WindowLeft;
      this.Height      = _settings.WindowHeight;
      this.Width       = _settings.WindowLWidth;
      this.WindowState = (WindowState)_settings.WindowState;
      _mainRow.Height  = new GridLength(_settings.TabControlHeight);


      _configComboBox.ItemsSource = new[] {"Debug", "Release"};
      var config = _settings.Config;
      _configComboBox.SelectedItem = config == "Release" ? "Release" : "Debug";

      _tabControl.SelectedIndex = _settings.ActiveTabIndex;
      _foldingStrategy          = new NitraFoldingStrategy();
      _textBox1Tooltip          = new ToolTip { PlacementTarget = _text };
      _parseTimer               = new Timer { AutoReset = false, Enabled = false, Interval = 300 };
      _parseTimer.Elapsed      += _parseTimer_Elapsed;

      _text.TextArea.Caret.PositionChanged += Caret_PositionChanged;

      _highlightingStyles = new Dictionary<string, HighlightingColor>(StringComparer.OrdinalIgnoreCase)
      {
        { "Keyword",              new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Blue) } },
        { "Comment",              new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Green) } },
        { "InlineComment",        new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Green) } },
        { "MultilineComment",     new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Green) } },
        { "Number",               new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Magenta) } },
        { "Operator",             new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Navy) } },
        { "OpenBrace",            new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Navy) } },
        { "CloseBrace",           new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Navy) } },
        { "String",               new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Maroon) } },
        { "StringEx",             new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Maroon) } },
        { "Char",                 new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.DarkRed) } },
        { "Marker",               new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.LightBlue) } },
        { "NitraCSharpType",      new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.DarkCyan) } },
        { "NitraCSharpNamespace", new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Chocolate) } },
        { "NitraCSharpAlias",     new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.DarkViolet) } },
        { "Error",                new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Red) } },
      };

      _foldingManager    = FoldingManager.Install(_text.TextArea);
      _textMarkerService = new TextMarkerService(_text.Document);

      _text.TextArea.TextView.BackgroundRenderers.Add(_textMarkerService);
      _text.TextArea.TextView.LineTransformers.Add(_textMarkerService);
      _text.Options.ConvertTabsToSpaces = true;
      _text.Options.EnableRectangularSelection = true;
      _text.Options.IndentationSize = 2;
      _testsTreeView.SelectedValuePath = "FullPath";
      _propertyGrid = new PropertyGrid();
      _windowsFormsHost.Child = _propertyGrid;

      _cmpilerMessages = new VisualizerCompilerMessages(_errorsTreeView.Items, _textMarkerService, _text);

      if (string.IsNullOrWhiteSpace(_settings.CurrentSolution))
        _solution = null;
      else
        LoadTests();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      _loading = false;

      if ((Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) != 0)
        return;
      if (_solution != null)
        SelectTest(_settings.SelectedTestSuit, _settings.SelectedTest);
    }

    private void Window_Closed(object sender, EventArgs e)
    {
      _settings.Config           = (string)_configComboBox.SelectedValue;
      _settings.TabControlHeight = _mainRow.Height.Value;
      _settings.LastTextInput    = _text.Text;
      _settings.WindowState      = (int)this.WindowState;
      _settings.WindowTop        = this.Top;
      _settings.WindowLeft       = this.Left;
      _settings.WindowHeight     = this.Height;
      _settings.WindowLWidth     = this.Width;
      _settings.ActiveTabIndex   = _tabControl.SelectedIndex;

      SaveSelectedTestAndTestSuit();
    }

    private void SaveSelectedTestAndTestSuit()
    {
      if (_currentTestSuit != null)
      {
        _settings.SelectedTestSuit = _currentTestSuit.TestSuitPath;
        var test = _testsTreeView.SelectedItem as TestVm;
        _settings.SelectedTest = test == null ? null : test.Name;
      }

      if (_solution != null && _solution.IsDirty)
        _solution.Save();
    }

    private void LoadTests()
    {
      var selected = _testsTreeView.SelectedItem as FullPathVm;
      var selectedPath     = selected == null ? null : selected.FullPath;

      if (!File.Exists(_settings.CurrentSolution ?? ""))
      {
        MessageBox.Show(this, "Solution '" + _settings.CurrentSolution + "' not exists!");
        return;
      }

      _solution = new SolutionVm(_settings.CurrentSolution, selectedPath, _settings.Config, _cmpilerMessages);
      this.Title = _solution.Name + " - " + Constants.AppName;
      _testsTreeView.ItemsSource = _solution.TestSuits;
    }

    private void textBox1_GotFocus(object sender, RoutedEventArgs e)
    {
      ShowNodeForCaret();
    }

    void Caret_PositionChanged(object sender, EventArgs e)
    {
      var caretPos = _text.CaretOffset;
      _pos.Text = caretPos.ToString(CultureInfo.InvariantCulture);
      TryHighlightBraces(caretPos);
      ShowNodeForCaret();
    }

    private void TryHighlightBraces(int caretPos)
    {
      if (_parseResult == null)
        return;

      if (_matchedBracketsMarkers.Count > 0)
      {
        foreach (var marker in _matchedBracketsMarkers)
          _textMarkerService.Remove(marker);
        _matchedBracketsMarkers.Clear();
      }

      var context = new MatchBracketsWalker.Context(caretPos);
      _matchBracketsWalker.Walk(_parseResult, context);
      _matchedBrackets = context.Brackets;

      if (context.Brackets != null)
      {
        foreach (var bracket in context.Brackets)
        {
          var marker1 = _textMarkerService.Create(bracket.OpenBracket.StartPos, bracket.OpenBracket.Length);
          marker1.BackgroundColor = Colors.LightGray;
          _matchedBracketsMarkers.Add(marker1);

          var marker2 = _textMarkerService.Create(bracket.CloseBracket.StartPos, bracket.CloseBracket.Length);
          marker2.BackgroundColor = Colors.LightGray;
          _matchedBracketsMarkers.Add(marker2);
        }
      }
    }

    private void ShowNodeForCaret()
    {
      if (_doTreeOperation)
        return;

      _doChangeCaretPos = true;
      try
      {
        if      (object.ReferenceEquals(_tabControl.SelectedItem, _declarationsTabItem))
          ShowAstNodeForCaret();
        else if (object.ReferenceEquals(_tabControl.SelectedItem, _reflectionTabItem))
          ShowParseTreeNodeForCaret();
      }
      finally
      {
        _doChangeCaretPos = false;
      }
    }

    private void ShowAstNodeForCaret()
    {
      if (_declarationsTreeView.IsKeyboardFocusWithin)
        return;

      if (_declarationsTreeView.Items.Count < 1)
        return;

      Debug.Assert(_declarationsTreeView.Items.Count == 1);

      var result = FindNode((TreeViewItem)_declarationsTreeView.Items[0], _text.CaretOffset);
      if (result == null)
        return;

      result.IsSelected = true;
      result.BringIntoView();
      //_reflectionTreeView.BringIntoView(result);
    }

    private TreeViewItem FindNode(TreeViewItem item, int pos)
    {
      var ast = item.Tag as IAst;

      if (ast == null)
        return null;

      if (ast.Span.IntersectsWith(pos))
      {
        item.IsExpanded = true;
        foreach (TreeViewItem subItem in item.Items)
        {
          var result = FindNode(subItem, pos);
          if (result != null)
            return result;
        }

        return item;
      }

      return null;
    }

    private void ShowParseTreeNodeForCaret()
    {
      if (_reflectionTreeView.ItemsSource == null)
        return;

      if (_reflectionTreeView.IsKeyboardFocusWithin)
        return;


      var node = FindNode((ReflectionStruct[])_reflectionTreeView.ItemsSource, _text.CaretOffset);

      if (node != null)
      {
        var selected = _reflectionTreeView.SelectedItem as ReflectionStruct;

        if (node == selected)
          return;

        _reflectionTreeView.SelectedItem = node;
        _reflectionTreeView.BringIntoView(node);
      }
    }

    private ReflectionStruct FindNode(IEnumerable<ReflectionStruct> items, int p)
    {
      foreach (ReflectionStruct node in items)
      {
        if (node.Span.StartPos <= p && p < node.Span.EndPos) // IntersectsWith(p) includes EndPos
        {
          if (node.Children.Count == 0)
            return node;

          _reflectionTreeView.Expand(node);

          return FindNode(node.Children, p);
        }
      }

      return null;
    }

    private class VisualizerCompilerMessages : ICompilerMessages, IRootCompilerMessages
    {
      private Guid _kind;
      private ItemCollection _errors;
      private TextMarkerService _textMarkerService;
      private NitraTextEditor _text;

      public VisualizerCompilerMessages(ItemCollection errors, TextMarkerService textMarkerService, NitraTextEditor text)
      {
        _errors = errors;
        _textMarkerService = textMarkerService;
        _text = text;
      }

      public void Clear()
      {
        _errors.Clear();
        _textMarkerService.RemoveAll(m => m.Tag.Equals(ErrorMarkerTag));
      }

      public void ReportMessage(CompilerMessageType messageType, Location loc, string msg, int num)
      {
        var location = loc;
        var marker = _textMarkerService.Create(location.StartPos, location.Length);
        marker.Tag = ErrorMarkerTag;
        marker.MarkerType = TextMarkerType.SquigglyUnderline;
        marker.MarkerColor = Colors.Red;
        marker.ToolTip = msg;

        var errorNode = new TreeViewItem();
        errorNode.Tag = _kind;
        errorNode.Header = "(" + location.EndLineColumn + "): " + msg;
        errorNode.MouseDoubleClick += (sender, e) =>
        {
          _text.CaretOffset = loc.StartPos;
          _text.Select(loc.StartPos, loc.Length);
          _text.ScrollToLine(loc.StartLineColumn.Line);
        };

        _errors.Add(errorNode);
      }

      public IRootCompilerMessages ReportRootMessage(CompilerMessageType messageType, Location loc, string msg, int num)
      {
        Debug.Assert(_errors.Count > 0);
        var node = (TreeViewItem)_errors[_errors.Count - 1];
        var nested = new VisualizerCompilerMessages(node.Items, _textMarkerService, _text);
        return nested;
      }

      public void SetFutureMessagesKind(Guid kind)
      {
        _kind = kind;
      }

      public void Remove(Func<Guid, Location, bool> predicate)
      {
        for (int i = 0; i < _errors.Count;)
        {
          TreeViewItem errorNode = (TreeViewItem)_errors[i];
          var guid = (Guid)errorNode.Tag;
          if (guid == TestVm.TypingMsg)
            _errors.RemoveAt(i);
          else
            i++;
        }
      }

      public void Dispose() { }
    }

    private void TryReportError()
    {
      ClearMarkers();

      _errorsTreeView.Items.Clear();

      if (_parseResult == null)
        if (_currentTestSuit.Exception != null)
        {
          var msg = "Exception: " + _currentTestSuit.Exception.Message;
          _status.Text = msg;

          var errorNode = new TreeViewItem();
          errorNode.Header = "(1,1): " + msg;
          errorNode.Tag = _currentTestSuit.Exception;
          errorNode.MouseDoubleClick += errorNode_MouseDoubleClick;
          _errorsTreeView.Items.Add(errorNode);

          var marker = _textMarkerService.Create(0, _text.Text.Length);
          marker.Tag = ErrorMarkerTag;
          marker.MarkerType = TextMarkerType.SquigglyUnderline;
          marker.MarkerColor = Colors.Purple;
          marker.ToolTip = msg;
        }
        else
          _status.Text = "Not parsed!";
      else
      {
        var errors = _parseResult.GetErrors();
        var errorNodes = _errorsTreeView.Items;

        if (errors.Length == 0)
        {
          _status.Text = "OK";
          return;
        }


        foreach (var error in errors)
        {
          var location = error.Location;
          var marker = _textMarkerService.Create(location.StartPos, location.Length);
          marker.Tag = ErrorMarkerTag;
          marker.MarkerType = TextMarkerType.SquigglyUnderline;
          marker.MarkerColor = Colors.Red;
          string text;
          try { text = error.DebugText; } catch { text = ""; }
          marker.ToolTip = error.Message + "\r\n\r\n" + text;

          var errorNode = new TreeViewItem();
          errorNode.Header = "(" + error.Location.EndLineColumn + "): " + error.ToString();
          errorNode.Tag = error;
          errorNode.MouseDoubleClick += errorNode_MouseDoubleClick;

          var subNode = new TreeViewItem();
          subNode.FontSize = 12;
          subNode.Header = error.DebugText;
          errorNode.Items.Add(subNode);

          errorNodes.Add(errorNode);
        }

        _status.Text = "Parsing completed with " + errors.Length + " error[s]";
      }
    }

    void errorNode_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      var node = (TreeViewItem)sender;
      var error = node.Tag as ParseError;
      if (error != null)
      {
        _text.CaretOffset = error.Location.StartPos;
        _text.Select(error.Location.StartPos, error.Location.Length);
        _text.ScrollToLine(error.Location.StartLineColumn.Line);
      }
      else
      {
        _text.CaretOffset = 0;
      }
      e.Handled = true;
      _text.Focus();
    }


    void ShowInfo()
    {
      _needUpdateReflection      = true;
      _needUpdateHtmlPrettyPrint = true;
      _needUpdateTextPrettyPrint = true;
      _needUpdatePerformance     = true;
      _parseTree                 = null;

      UpdateInfo();
    }

    void UpdateInfo()
    {
      try
      {
        if (_needUpdateReflection           && object.ReferenceEquals(_tabControl.SelectedItem, _reflectionTabItem))
          UpdateReflection();
        else if (_needUpdateHtmlPrettyPrint && object.ReferenceEquals(_tabControl.SelectedItem, _htmlPrettyPrintTabItem))
          UpdateHtmlPrettyPrint();
        else if (_needUpdateTextPrettyPrint && object.ReferenceEquals(_tabControl.SelectedItem, _textPrettyPrintTabItem))
          UpdateTextPrettyPrint();
        else if (_needUpdatePerformance     && object.ReferenceEquals(_tabControl.SelectedItem, _performanceTabItem))
          UpdatePerformance();
        
        UpdateDeclarations();
      }
      catch(Exception e)
      {
        Debug.Write(e);
      }
    }

    private void UpdatePerformance()
    {
      _needUpdatePerformance = false;
      if (object.ReferenceEquals(_tabControl.SelectedItem, _performanceTabItem))
        UpdateParseTree();
    }

    private void UpdateParseTree()
    {
      if (_parseResult == null)
        return;
      if (IsSplicable(_parseResult))
        return;

      if (_parseTree == null)
      {
        var stat = ((StatisticsTask.Container [])_performanceTreeView.ItemsSource)[0];
        var createParseTreeStat = new StatisticsTask.Single("ParseTree", "Create Parse Tree");
        stat.AddSubtask(createParseTreeStat);
        createParseTreeStat.Start();
        _parseTree = _parseResult.CreateParseTree();
        createParseTreeStat.Stop();
      }
    }

    private void UpdateReflection()
    {
      _needUpdateReflection = false;

      if (_parseResult == null)
        return;

      var root = _parseResult.Reflect();
      _reflectionTreeView.ItemsSource = new[] { (ReflectionStruct)root };
    }

    private void UpdateHtmlPrettyPrint()
    {
      _needUpdateHtmlPrettyPrint = false;

      if (_parseResult == null)
        return;
      if (IsSplicable(_parseResult))
        return;

      if (_parseTree == null)
        _parseTree = _parseResult.CreateParseTree();

      var htmlWriter = new HtmlPrettyPrintWriter(PrettyPrintOptions.DebugIndent | PrettyPrintOptions.MissingNodes, "missing", "debug", "garbage");
      _parseTree.PrettyPrint(htmlWriter, 0, null);
      var html = Properties.Resources.PrettyPrintDoughnut.Replace("{prettyprint}", htmlWriter.ToString());
      prettyPrintViewer.NavigateToString(html);
    }

    private void UpdateTextPrettyPrint()
    {
      _needUpdateTextPrettyPrint = false;

      if (_parseResult == null)
        return;
      if (IsSplicable(_parseResult))
        return;

      if (_parseTree == null)
        _parseTree = _parseResult.CreateParseTree();

      _prettyPrintTextBox.Text = _parseTree.ToString(PrettyPrintOptions.DebugIndent | PrettyPrintOptions.MissingNodes);
    }

    private void textBox1_TextChanged(object sender, EventArgs e)
    {
      if (_loading)
        return;

      _parseResult = null; // prevent calculations on outdated ParseResult
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

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
      this.Close();
    }

    private void FileOpenExecuted(object sender, RoutedEventArgs e)
    {
      var dialog = new OpenFileDialog { Filter = "C# (.cs)|*.cs|Nitra (.nitra)|*.nitra|JSON (.json)|*.json|Text (.txt)|*.txt|All|*.*" };
// ReSharper disable once ConstantNullCoalescingCondition
      if (dialog.ShowDialog(this) ?? false)
      {
        _text.Text = File.ReadAllText(dialog.FileName);
      }
    }

    void _parseTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
      Dispatcher.Invoke(new Action(DoParse));
    }

    private RecoveryAlgorithm GetRecoveryAlgorithm()
    {
      if (_recoveryAlgorithmSmart.IsChecked == true)
        return RecoveryAlgorithm.Smart;
      if (_recoveryAlgorithmPanic.IsChecked == true)
        return RecoveryAlgorithm.Panic;
      if (_recoveryAlgorithmFirstError.IsChecked == true)
        return RecoveryAlgorithm.FirstError;
      return RecoveryAlgorithm.Smart;
    }

    private void DoParse()
    {
      if (_doTreeOperation)
        return;

      _astRoot = null;
      _parseResult = null;

      if (_currentTestSuit == null || _currentTest == null)
        return;

      try
      {
        _cmpilerMessages.Clear();
        _recoveryTreeView.Items.Clear();
        _errorsTreeView.Items.Clear();
        _reflectionTreeView.ItemsSource = null;
        var timer = Stopwatch.StartNew();

        _currentTest.Code = _text.Text;
        _currentTest.Run(GetRecoveryAlgorithm());
        _performanceTreeView.ItemsSource = new[] { (_currentTest.Statistics ?? _currentTestFolder.Statistics) };

        _astRoot = _currentTest.File.Ast;
        _parseResult = _currentTest.File.ParseResult;
        _foldingStrategy.ParseResult = _parseResult;
        _foldingStrategy.UpdateFoldings(_foldingManager, _text.Document);

        TryHighlightBraces(_text.CaretOffset);
        TryReportError();
        ShowInfo();

        _text.TextArea.TextView.Redraw(DispatcherPriority.Input);
      }
      catch (Exception ex)
      {
        ClearMarkers();
        MessageBox.Show(this, ex.GetType().Name + ":" + ex.Message);
        Debug.WriteLine(ex.ToString());
      }
    }

    private void ClearMarkers()
    {
      _textMarkerService.RemoveAll(marker => marker.Tag == (object)ErrorMarkerTag);
    }

    private class CollectSymbolsAstVisitor : IAstVisitor
    {
      private readonly NSpan _span;
      public List<SpanInfo> SpanInfos { get; private set; }

      public CollectSymbolsAstVisitor(NSpan span) { _span = span; SpanInfos = new List<SpanInfo>(); }

      public void Visit(IAst parseTree)
      {
        if (parseTree.Span.IntersectsWith(_span))
          parseTree.Apply(this);
      }

      public void Visit(IReference reference)
      {
        var span = reference.Span;

        if (!span.IntersectsWith(_span) || !reference.IsSymbolEvaluated)
          return;
        
        var sym = reference.Symbol;
        var spanClass = sym.SpanClass;
        
        if (spanClass == "Default")
          return;

        SpanInfos.Add(new SpanInfo(span, new SpanClass(sym.SpanClass)));
      }
    }

    private void textBox1_HighlightLine(object sender, HighlightLineEventArgs e)
    {
      if (_parseResult == null)
        return;

      try
      {
        var timer = Stopwatch.StartNew();
        var line = e.Line;
        var spans = new HashSet<SpanInfo>();
        _parseResult.GetSpans(line.Offset, line.EndOffset, spans);
        var astRoot = _astRoot;
        if (astRoot != null)
        {
          var visitor = new CollectSymbolsAstVisitor(new NSpan(line.Offset, line.EndOffset));
          astRoot.Apply(visitor);
          foreach (var spanInfo in visitor.SpanInfos)
            spans.Add(spanInfo);
        }
        _highlightingTimeSpan = timer.Elapsed;

        foreach (var span in spans)
        {
          HighlightingColor color;
          if (_highlightingStyles.TryGetValue(span.SpanClass.Name, out color))
          {
            var startOffset = Math.Max(line.Offset, span.Span.StartPos);
            var endOffset = Math.Min(line.EndOffset, span.Span.EndPos);
            var section = new HighlightedSection
            {
              Offset = startOffset,
              Length = endOffset - startOffset,
              Color = color
            };
            e.Sections.Add(section);
          }
          else
            Debug.WriteLine("Span class '" + span.SpanClass.Name + "' not found in styles");
        }
      }
      catch (Exception ex) { Debug.WriteLine(ex.GetType().Name + ":" + ex.Message); }
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

    private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = true;
      e.Handled = true;
    }

    private void _tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      UpdateInfo();
      ShowNodeForCaret();
    }

    private static string MakeStr(string str, int maxLen)
    {
      var padding = maxLen - str.Length + 1;
      return new string(' ', padding) + str;
    }

    private void _copyButton_Click(object sender, RoutedEventArgs e)
    {
      var sb = new StringBuilder();
      var stats = ((StatisticsTask.Container[])_performanceTreeView.ItemsSource);

      foreach (var stat in stats)
        sb.AppendLine(stat.ToString());
      
      var result = sb.ToString();

      Clipboard.SetData(DataFormats.Text, result);
      Clipboard.SetData(DataFormats.UnicodeText, result);
    }

    private void CopyReflectionNodeText(object sender, ExecutedRoutedEventArgs e)
    {
      var value = _reflectionTreeView.SelectedItem as ReflectionStruct;

      if (value != null)
      {
        var result = value.Description;
        Clipboard.SetData(DataFormats.Text, result);
        Clipboard.SetData(DataFormats.UnicodeText, result);
      }
    }

    bool CheckTestFolder()
    {
      if (File.Exists(_settings.CurrentSolution ?? ""))
        return true;

      return false;
    }

    private void OnAddTest(object sender, ExecutedRoutedEventArgs e)
    {
      if (CheckTestFolder())
        AddTest();
      else
        MessageBox.Show(this, "Can't add test.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
    }


    private void CommandBinding_CanAddTest(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = _currentTestSuit != null;
      e.Handled = true;
    }

    private void AddTest()
    {
      if (_currentTestSuit == null)
      {
        MessageBox.Show(this, "Select a test suit first.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

      if (_needUpdateTextPrettyPrint)
        UpdateTextPrettyPrint();
      var testSuitPath = _currentTestSuit.TestSuitPath;
      var dialog = new AddTest(TestFullPath(testSuitPath), _text.Text, _prettyPrintTextBox.Text) { Owner = this };
      if (dialog.ShowDialog() ?? false)
      {
        var testName = dialog.TestName;
        LoadTests();
        SelectTest(testSuitPath, testName);
      }
    }

    private void SelectTest(string testSuitPath, string testName)
    {
      if (!CheckTestFolder())
      {
        MessageBox.Show(this, "The test folder does not exists.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

      var testSuits = (ObservableCollection<TestSuitVm>) _testsTreeView.ItemsSource;

      if (testSuits == null)
        return;

      var result = testSuits.FirstOrDefault(ts => ts.FullPath == testSuitPath);
      if (result == null)
        return;
      var test = result.Tests.FirstOrDefault(t => t.Name == testName);
      if (test != null)
        test.IsSelected = true;
      else
        result.IsSelected = true;
    }

    private static string TestFullPath(string path)
    {
      return Path.GetFullPath(path);
    }

    private void OnRunTests(object sender, ExecutedRoutedEventArgs e)
    {
      if (CheckTestFolder())
        RunTests();
      else
        MessageBox.Show(this, "Can't run tests.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void RunTests()
    {
      if (_testsTreeView.ItemsSource == null)
        return;

      var testSuits = (ObservableCollection<TestSuitVm>)_testsTreeView.ItemsSource;

      foreach (var testSuit in testSuits)
      {
        foreach (var test in testSuit.Tests)
          RunTest(test);

        testSuit.TestStateChanged();
      }
    }

    private void RunTest(ITest test)
    {
      var testFile = test as TestVm;
      if (testFile != null)
        RunTest(testFile);

      var testFolder = test as TestFolderVm;
      if (testFolder != null)
        RunTest(testFolder);
    }

    private void RunTest(TestFolderVm testFolder)
    {
      foreach (var testFile in testFolder.Tests)
        RunTest(testFile);
    }

    private void RunTest(TestVm test)
    {
      test.Run(recoveryAlgorithm: GetRecoveryAlgorithm());

      ShowDiff(test);
    }

    private void ShowDiff(TestVm test)
    {
      _para.Inlines.Clear();

      if (test.PrettyPrintResult == null)
      {
        _para.Inlines.AddRange(new Inline[] { new Run("The test was never started.") { Foreground = Brushes.Gray } });
        return;
      }

      if (test.TestState == TestState.Failure)
      {
        var lines = Diff(Split(test.Gold), Split(test.PrettyPrintResult));
        lines.RemoveAt(0);
        lines.RemoveAt(lines.Count - 1);

        foreach (var line in lines)
          _para.Inlines.AddRange(line);
      }
      else if (test.TestState == TestState.Success)
        _para.Inlines.AddRange(new Inline[] { new Run("Output of the test and the 'gold' are identical.") { Foreground = Brushes.LightGreen } });
    }

    private static string[] Split(string gold)
    {
      return gold.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
    }

    private static List<Inline[]> Diff(string[] textA, string[] textB, int rangeToShow = 3)
    {
      var indexA = 0;
      var output = new List<Inline[]> { MakeLine("BEGIN-DIFF", Brushes.LightGray) };

      foreach (var diffItem in textA.Diff(textB))
      {
        //в начале итерации indexA содержит индекс строки идущей сразу за предыдущим блоком

        // определяем нужно ли выводить разделитель
        if (diffItem.Index - indexA > rangeToShow * 2)
        {
          //показываем строки идущие после предыдущего блока
          for (var i = 0; i < rangeToShow; ++i)
          {
            output.Add(MakeLine(textA[indexA]));
            ++indexA;
          }

          output.Add(MakeLine("...", Brushes.LightGray));

          //показываем строки идущие перед текущим блоком
          indexA = diffItem.Index - rangeToShow;
          for (var i = 0; i < rangeToShow; ++i)
          {
            output.Add(MakeLine(textA[indexA]));
            ++indexA;
          }
        }
        else
        {
          //показываем строки между блоками
          while (indexA < diffItem.Index)
          {
            output.Add(MakeLine(textA[indexA]));
            ++indexA;
          }
        }

        // показываем удаленные строки
        for (var i = 0; i < diffItem.Deleted; ++i)
        {
          output.Add(MakeLine(textA[indexA], Brushes.LightPink));
          ++indexA;
        }

        // показываем добавленные строки
        foreach (var insertedItem in diffItem.Inserted)
          output.Add(MakeLine(insertedItem, Brushes.LightGreen));
      }

      // показываем не более rangeToShow последующих строк
      var tailLinesToShow = Math.Min(rangeToShow, textA.Length - indexA);

      for (var i = 0; i < tailLinesToShow; ++i)
      {
        output.Add(MakeLine(textA[indexA]));
        ++indexA;
      }

      if (indexA < textA.Length)
        output.Add(MakeLine("...", Brushes.LightGray));

      output.Add(MakeLine("END-DIFF", Brushes.LightGray));

      return output;
    }

    private static Inline[] MakeLine(string text, Brush brush = null)
    {
      return new Inline[]
      {
        brush == null ? new Run(text) : new Run(text) { Background = brush },
        new LineBreak()
      };
    }

    private void OnAddTestSuit(object sender, ExecutedRoutedEventArgs e)
    {
      EditTestSuit(true);
    }

    private void EditTestSuit(bool create)
    {
      if (_solution == null)
        return;
      var currentTestSuit = _currentTestSuit;
      var dialog = new TestSuitDialog(create, currentTestSuit) { Owner = this };
      if (dialog.ShowDialog() ?? false)
      {
        if (currentTestSuit != null)
          _solution.TestSuits.Remove(currentTestSuit);
        var testSuit = new TestSuitVm(_solution, dialog.TestSuitName, _settings.Config, _cmpilerMessages);
        testSuit.IsSelected = true;
        _solution.Save();
      }
    }


    private void OnRemoveTest(object sender, ExecutedRoutedEventArgs e)
    {
      var test = _testsTreeView.SelectedItem as TestVm;
      if (test == null)
        return;
      if (MessageBox.Show(this, "Do you want to delete the '" + test.Name + "' test?", "Visualizer!", MessageBoxButton.YesNo,
        MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
        return;

      var fullPath = TestFullPath(test.TestPath);
      File.Delete(fullPath);
      var goldFullPath = Path.ChangeExtension(fullPath, ".gold");
      if (File.Exists(goldFullPath))
        File.Delete(goldFullPath);
      LoadTests();
    }

    private void CommandBinding_CanRemoveTest(object sender, CanExecuteRoutedEventArgs e)
    {
      if (_testsTreeView == null)
        return;
      e.CanExecute = _testsTreeView.SelectedItem is TestVm;
      e.Handled = true;
    }

    private void _testsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
      var test = e.NewValue as TestVm;
      if (test != null)
      {
        _text.Text = test.Code;
        _currentTestSuit = test.TestSuit;
        ShowDiff(test);

      }

      var testSuit = e.NewValue as TestSuitVm;
      if (testSuit != null)
      {
        _text.Text = "";
        _currentTestSuit = testSuit;
        _para.Inlines.Clear();
      }

      SaveSelectedTestAndTestSuit();

      _settings.Save();
    }

    private void OnRemoveTestSuit(object sender, ExecutedRoutedEventArgs e)
    {
      if (_solution == null || _currentTestSuit == null)
        return;

      if (MessageBox.Show(this, "Do you want to delete the '" + _currentTestSuit.Name + "' test suit?\r\nAll test will be deleted!", "Visualizer!", MessageBoxButton.YesNo,
        MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
        return;

      _currentTestSuit.Remove();
    }

    private void CommandBinding_CanRemoveTestSuit(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = _currentTestSuit != null;
      e.Handled = true;
    }

    private void OnRunTest(object sender, ExecutedRoutedEventArgs e)
    {
      RunTest();
    }

    private void RunTest()
    {
      {
        var test = _testsTreeView.SelectedItem as TestVm;
        if (test != null)
        {
          RunTest(test);
          test.TestSuit.TestStateChanged();

          if (test.TestState == TestState.Failure)
            _testResultDiffTabItem.IsSelected = true;
        }
      }
      var testSuit = _testsTreeView.SelectedItem as TestSuitVm;
      if (testSuit != null)
      {
        foreach (var test in testSuit.Tests)
          RunTest(test);
        testSuit.TestStateChanged();
      }
    }

    private void CommandBinding_CanRunTest(object sender, CanExecuteRoutedEventArgs e)
    {
      if (_testsTreeView == null)
        return;

      if (_testsTreeView.SelectedItem is TestVm)
      {
        e.CanExecute = true;
        e.Handled = true;
      }
      else if (_testsTreeView.SelectedItem is TestSuitVm)
      {
        e.CanExecute = true;
        e.Handled = true;
      }
    }

    private void _testsTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      RunTest();
      e.Handled = true;
    }

    private void _configComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (_loading)
        return;

      var config = (string)_configComboBox.SelectedItem;
      _settings.Config = config;
      LoadTests();
    }

    private void OnUpdateTest(object sender, ExecutedRoutedEventArgs e)
    {
      var test = _testsTreeView.SelectedItem as TestVm;
      if (test != null)
      {
        try
        {
          test.Update(_text.Text, _prettyPrintTextBox.Text);
          test.TestSuit.TestStateChanged();
        }
        catch (Exception ex)
        {
          MessageBox.Show(this, "Fail to update the test '" + test.Name + "'." + Environment.NewLine + ex.GetType().Name + ":" + ex.Message, "Visualizer!",
            MessageBoxButton.YesNo,
            MessageBoxImage.Error);
        }
      }
    }

    private void OnReparse(object sender, ExecutedRoutedEventArgs e)
    {
      Dispatcher.Invoke(new Action(DoParse));
    }

    private void OnTreeViewItemSelected(object sender, RoutedEventArgs e)
    {
      if (!Object.ReferenceEquals(sender, e.OriginalSource))
        return;

      var item = e.OriginalSource as TreeViewItem;
      if (item != null)
        item.BringIntoView();
    }

    private void _reflectionTreeView_SelectedItemChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
      if (_doChangeCaretPos)
        return;

      var node = e.NewValue as ReflectionStruct;

      if (node == null)
        return;

      if (_text.IsKeyboardFocusWithin)
        return;

      _doTreeOperation = true;
      try
      {
        _text.TextArea.Caret.Offset = node.Span.StartPos;
        _text.ScrollTo(_text.TextArea.Caret.Line, _text.TextArea.Caret.Line);
        _text.TextArea.AllowCaretOutsideSelection();
        _text.Select(node.Span.StartPos, node.Span.Length);
      }
      finally
      {
        _doTreeOperation = false;
      }
    }

    private void OnShowGrammar(object sender, ExecutedRoutedEventArgs e)
    {
      if (_currentTestSuit == null)
        return;

      _currentTestSuit.ShowGrammar();
    }

    private void CommandBinding_CanShowGrammar(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = _currentTestSuit != null;
      e.Handled = true;
    }

    private static bool IsSplicable(IParseResult parseResult)
    {
      return parseResult.ParseSession.StartRuleDescriptor.Grammar.IsSplicable;
    }

    private CompletionWindow _completionWindow;

    private void _control_KeyDown_resize(object sender, KeyEventArgs e)
    {
      var control = sender as Control;
      if (control != null)
      {
        if (e.Key == Key.Add && Keyboard.Modifiers == ModifierKeys.Control)
          control.FontSize++;
        else if (e.Key == Key.Subtract && Keyboard.Modifiers == ModifierKeys.Control)
          control.FontSize--;

        if (Keyboard.Modifiers != ModifierKeys.Control)
          return;

        if (Keyboard.IsKeyDown(Key.Space))
        {
          int end = _text.CaretOffset;
          int start = end;
          var line = _text.Document.GetLineByOffset(end);
          var lineText = _text.Document.GetText(line);
          var offsetInLine = end - 1 - line.Offset;
          for (int i = offsetInLine; i >= 0; i--)
          {
            var ch = lineText[i];
            if (!char.IsLetter(ch))
            {
              break;
            }
            start--;
          }
          var text = _text.Text.Substring(0, start) + '\xFFFF';
          var prefix = _text.Document.GetText(start, end - start);
          _currentTestSuit.Run(text, null, start, prefix);
          _performanceTreeView.ItemsSource = new[] { _currentTestSuit.Statistics };
          var ex = _currentTestSuit.Exception;
          var result = ex as LiteralCompletionException;
          //MessageBox.Show(string.Join(", ", result.Literals.OrderBy(x => x)));
          if (result == null)
            return;

          _completionWindow = new CompletionWindow(_text.TextArea);
          IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;
          var span = new NSpan(start, end);
          foreach (var literal in result.Literals)
            data.Add(new LiteralCompletionData(span, literal));

          _completionWindow.Show();
          _completionWindow.Closed +=
            delegate { _completionWindow = null; };
          e.Handled = true;
        }
        else if (e.Key == Key.Oem6) // Oem6 - '}'
          TryMatchBraces();
      }
    }

    private void TryMatchBraces()
    {
      var pos = _text.CaretOffset;
      foreach (var bracket in _matchedBrackets)
      {
        if (TryMatchBrace(bracket.OpenBracket, pos, bracket.CloseBracket.EndPos))
          break;
        if (TryMatchBrace(bracket.CloseBracket, pos, bracket.OpenBracket.StartPos))
          break;
      }
    }

    private bool TryMatchBrace(NSpan brace, int pos, int gotoPos)
    {
      if (!brace.IntersectsWith(pos))
        return false;

      _text.CaretOffset = gotoPos;
      _text.ScrollToLine(_text.TextArea.Caret.Line);
      return true;
    }

    private void _testsTreeView_CopyNodeText(object sender, RoutedEventArgs e)
    {
      CopyTreeNodeToClipboard(_testsTreeView.SelectedItem);
    }

    private void _errorsTreeView_CopyNodeText(object sender, RoutedEventArgs e)
    {
      CopyTreeNodeToClipboard(((TreeViewItem)_errorsTreeView.SelectedItem).Header);
    }

    private static void CopyTreeNodeToClipboard(object node)
    {
      if (node != null)
      {
        var text = node.ToString();
        Clipboard.SetData(DataFormats.Text, text);
        Clipboard.SetData(DataFormats.UnicodeText, text);
      }
    }

    private void OnSolutionNew(object sender, ExecutedRoutedEventArgs e)
    {
      var dialog = new System.Windows.Forms.SaveFileDialog();
      using (dialog)
      {
        dialog.CheckFileExists = false;
        dialog.DefaultExt = "nsln";
        dialog.Filter = "Nitra visualizer solution (*.nsln)|*.nsln";
        //dialog.InitialDirectory = GetDefaultDirectory();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
          var solutionFilePath = dialog.FileName;
          try
          {
            File.WriteAllText(dialog.FileName, "", Encoding.UTF8);
          }
          catch (Exception ex)
          {
            MessageBox.Show(this, "Can't create the file '" + solutionFilePath + "'.\r\n" + ex.GetType().Name + ":" + ex.Message, Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
          }

          OpenSolution(solutionFilePath);
        }
      }
    }

    private void OpenSolution(string solutionFilePath)
    {
      _settings.CurrentSolution = solutionFilePath;
      _solution = new SolutionVm(solutionFilePath, null, _settings.Config, _cmpilerMessages);
      _testsTreeView.ItemsSource = _solution.TestSuits;
      RecentFileList.InsertFile(solutionFilePath);
    }

    private void OnSolutionOpen(object sender, ExecutedRoutedEventArgs e)
    {
      var dialog = new System.Windows.Forms.OpenFileDialog();
      using (dialog)
      {
        dialog.Multiselect = false;
        dialog.CheckFileExists = true;
        dialog.DefaultExt = "nsln";
        dialog.Filter = "Nitra visualizer solution (*.nsln)|*.nsln";
        //dialog.InitialDirectory = GetDefaultDirectory();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
          OpenSolution(dialog.FileName);
        }
      }
    }

    private ContextMenu _defaultContextMenu;

    private void OnAddExistsTestSuit(object sender, ExecutedRoutedEventArgs e)
    {
      var unattachedTestSuits = _solution.GetUnattachedTestSuits();
      var menu = new ContextMenu();
      foreach (var name in unattachedTestSuits)
      {
        var item = new MenuItem();
        item.Header = name;
        item.Click += item_Click;
        menu.Items.Add(item);
      }

      _defaultContextMenu = _testsTreeView.ContextMenu;
      _testsTreeView.ContextMenu = menu;
      menu.Closed += menu_Closed;
      menu.IsOpen = true;
    }

    void menu_Closed(object sender, RoutedEventArgs e)
    {
      _testsTreeView.ContextMenu = _defaultContextMenu;
    }

    void item_Click(object sender, RoutedEventArgs e)
    {
      var rootDir = Path.GetDirectoryName(_solution.SolutinFilePath) ?? "";
      var name = (string)((MenuItem)e.Source).Header;
      var testSuit = new TestSuitVm(_solution, name, _settings.Config, _cmpilerMessages);
      testSuit.IsSelected = true;
      _solution.Save();
    }

    private void CommandBinding_CanAddExistsTestSuit(object sender, CanExecuteRoutedEventArgs e)
    {
      Debug.WriteLine("CanAddExistsTestSuit");
      e.Handled = true;

      if (_solution == null)
      {
        e.CanExecute = false;
        return;
      }

      var unattachedTestSuits = _solution.GetUnattachedTestSuits();

      e.CanExecute = unattachedTestSuits.Length > 0;

      Debug.WriteLine("e.CanExecute = " + e.CanExecute);
    }

    private void CommandBinding_CanOnAddTestSuit(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = _solution != null;
      e.Handled = true;
    }

    private void OnEditTestSuit(object sender, ExecutedRoutedEventArgs e)
    {
      EditTestSuit(false);
    }

    private void CommandBinding_CanOnEditTestSuit(object sender, CanExecuteRoutedEventArgs e)
    {
      e.Handled = true;
      e.CanExecute = _currentTestSuit != null;
    }

    private void RecentFileList_OnMenuClick(object sender, RecentFileList.MenuClickEventArgs e)
    {
      OpenSolution(e.Filepath);
    }

    private void OnUsePanicRecovery(object sender, ExecutedRoutedEventArgs e)
    {
      OnReparse(null, null);
    }
  }
}
