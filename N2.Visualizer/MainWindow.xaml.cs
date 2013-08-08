using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
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
using N2.Visualizer.Properties;
using System.Diagnostics;
using System.Text;
using N2.Visualizer.ViewModels;
using Nemerle.Diff;
using RecoveryStack = Nemerle.Core.list<N2.Internal.RecoveryStackFrame>.Cons;

namespace N2.Visualizer
{
  using RecoveryInfo = Tuple<RecoveryResult, RecoveryResult[], RecoveryResult[], RecoveryStack[]>;
  using System.Windows.Documents;

  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow
  {
    bool _loading = true;
    Parser _parseResult;
    bool _doTreeOperation;
    bool _doChangeCaretPos;
    readonly Timer _parseTimer;
    readonly Dictionary<string, HighlightingColor> _highlightingStyles;
    readonly TextMarkerService _textMarkerService;
    readonly N2FoldingStrategy _foldingStrategy;
    readonly FoldingManager _foldingManager;
    readonly ToolTip _textBox1Tooltip;
    bool _needUpdateReflection;
    bool _needUpdateHtmlPrettyPrint;
    bool _needUpdateTextPrettyPrint;
    bool _needUpdatePerformance;
    Ast _ast;
    TimeSpan _parseTimeSpan;
    TimeSpan _astTimeSpan;
    TimeSpan _highlightingTimeSpan;
    readonly List<RecoveryInfo> _recoveryResults = new List<RecoveryInfo>();
    readonly Settings _settings;
    private TestSuitVm _currentTestSuit;

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

      var config = _settings.Config;

      _configComboBox.ItemsSource = new[] {"Debug", "Release"};
      _configComboBox.SelectedItem = config == "Release" ? "Release" : "Debug";

      _tabControl.SelectedIndex = _settings.ActiveTabIndex;
      _findGrid.Visibility      = System.Windows.Visibility.Collapsed;
      _foldingStrategy          = new N2FoldingStrategy();
      _textBox1Tooltip          = new ToolTip { PlacementTarget = _text };
      _parseTimer               = new Timer { AutoReset = false, Enabled = false, Interval = 300 };
      _parseTimer.Elapsed      += _parseTimer_Elapsed;

      _text.TextArea.Caret.PositionChanged += Caret_PositionChanged;

      _highlightingStyles = new Dictionary<string, HighlightingColor>
      {
        { "Keyword",  new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Blue) } },
        { "Comment",  new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Green) } },
        { "Number",   new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Magenta) } },
        { "Operator", new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Navy) } },
        { "String",   new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.Maroon) } },
      };

      _foldingManager    = FoldingManager.Install(_text.TextArea);
      _textMarkerService = new TextMarkerService(_text.Document);

      _text.TextArea.TextView.BackgroundRenderers.Add(_textMarkerService);
      _text.TextArea.TextView.LineTransformers.Add(_textMarkerService);

      _testsTreeView.SelectedValuePath = "FullPath";

      LoadTests();
    }

    private void LoadTests()
    {
      var selected = _testsTreeView.SelectedItem as FullPathVm;
      var path     = selected == null ? null : selected.FullPath;
      var testSuits = new ObservableCollection<TestSuitVm>();

      foreach (var dir in Directory.GetDirectories(_settings.TestsLocationRoot))
      {
        var testSuit = new TestSuitVm(_settings.TestsLocationRoot, dir);
        if (path != null)
        {
          if (testSuit.FullPath == path)
            testSuit.IsSelected = true; // Прикольно что по другому фокус не изменить!
          else foreach (var test in testSuit.Tests)
            if (test.FullPath == path)
              test.IsSelected = true;
        }
        testSuits.Add(testSuit);
      }

      _testsTreeView.ItemsSource = testSuits;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      SelectTest(_settings.SelectedTestSuit, _settings.SelectedTest);
      _loading = false;
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

      if (_currentTestSuit != null)
      {
        _settings.SelectedTestSuit = _currentTestSuit.TestSuitPath;
        var test = _testsTreeView.SelectedItem as TestVm;
        _settings.SelectedTest = test == null ? null : test.Name;
      }
      else
      {
        var text = _settings.LastTextInput;

        if (text != null)
          _text.Text = text;
      }
    }

    private void ReportRecoveryResult(RecoveryResult bestResult, List<RecoveryResult> bestResults, List<RecoveryResult> candidats, List<RecoveryStack> stacks)
    {
      _recoveryResults.Add(Tuple.Create(bestResult, bestResults.ToArray(), candidats.ToArray(), stacks.ToArray()));
    }

    private void textBox1_GotFocus(object sender, RoutedEventArgs e)
    {
      ShowNodeForCaret();
    }

    void Caret_PositionChanged(object sender, EventArgs e)
    {
      _pos.Text = _text.CaretOffset.ToString(CultureInfo.InvariantCulture);
      ShowNodeForCaret();
    }

    private void ShowNodeForCaret()
    {
      if (_doTreeOperation)
        return;

      if (!object.ReferenceEquals(_tabControl.SelectedItem, _reflectionTabItem))
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

      _errorsTreeView.Items.Clear();

      if (_parseResult == null)
        _status.Text = "Not parsed!";
      else if (_parseResult.IsSuccess)
      {
        _status.Text = "OK";
      }
      else
      {
        var errors = _parseResult.GetErrors();
        var errorNodes = _errorsTreeView.Items;

        foreach (ParseError error in errors)
        {
          var location = error.Location;
          var marker = _textMarkerService.Create(location.StartPos, location.Length);
          marker.MarkerType = TextMarkerType.SquigglyUnderline;
          marker.MarkerColor = Colors.Red;
          marker.ToolTip = error.Message + "\r\n\r\n" + error.DebugText;

          var errorNode = new TreeViewItem();
          errorNode.Header = "(" + error.Location.EndLineColumn + "): " + error.Message;
          errorNode.Tag = error;
          errorNode.MouseDoubleClick += errorNode_MouseDoubleClick;

          var subNode = new TreeViewItem();
          subNode.FontSize = 12;
          subNode.Header = error.DebugText;
          errorNode.Items.Add(subNode);

          errorNodes.Add(errorNode);
        }

        _status.Text = "Parsing completed with " + errors.Length + "error[s]";
      }
    }

    void errorNode_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      var node = (TreeViewItem)sender;
      var error = (ParseError)node.Tag;
      _text.CaretOffset = error.Location.StartPos;
      _text.Select(error.Location.StartPos, error.Location.Length);
      e.Handled = true;
      _text.Focus();
    }


    void ShowInfo()
    {
      _needUpdateReflection      = true;
      _needUpdateHtmlPrettyPrint = true;
      _needUpdateTextPrettyPrint = true;
      _needUpdatePerformance     = true;
      _ast                       = null;

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
      }
// ReSharper disable EmptyGeneralCatchClause
      catch { }
// ReSharper restore EmptyGeneralCatchClause
    }

    private void UpdatePerformance()
    {
      _needUpdatePerformance = false;
      if (_calcAstTime.IsChecked == true)
        UpdateAst();

      _totalTime.Text = (_parseTimeSpan + _astTimeSpan + _foldingStrategy.TimeSpan + _highlightingTimeSpan).ToString();
    }

    private void UpdateAst()
    {
      if (_ast == null)
      {
        var timer = Stopwatch.StartNew();
        _ast = _parseResult.CreateAst();
        _astTime.Text = (_astTimeSpan = timer.Elapsed).ToString();
      }
    }

    private void UpdateReflection()
    {
      _needUpdateReflection = false;

      treeView1.Items.Clear();

      if (_parseResult == null)
        return;

      var root = _parseResult.Reflect();
      var treeNode = new TreeViewItem();
      treeNode.Expanded += node_Expanded;
      treeNode.Header = root.Description;
      treeNode.Tag = root;
      treeNode.ContextMenu = (ContextMenu)Resources["TreeContextMenu"];
      if (root.Children.Count != 0)
        treeNode.Items.Add(new TreeViewItem());
      treeView1.Items.Add(treeNode);
    }

    private void UpdateHtmlPrettyPrint()
    {
      _needUpdateHtmlPrettyPrint = false;

      if (_ast == null)
        _ast = _parseResult.CreateAst();

      var htmlWriter = new HtmlPrettyPrintWriter(PrettyPrintOptions.DebugIndent | PrettyPrintOptions.MissingNodes, "missing", "debug");
      _ast.PrettyPrint(htmlWriter, 0);
      var html = Properties.Resources.PrettyPrintDoughnut.Replace("{prettyprint}", htmlWriter.ToString());
      prettyPrintViewer.NavigateToString(html);
    }

    private void UpdateTextPrettyPrint()
    {
      _needUpdateTextPrettyPrint = false;

      if (_parseResult == null)
      {
        return;
      }

      if (_ast == null)
        _ast = _parseResult.CreateAst();

      _prettyPrintTextBox.Text = _ast.ToString(PrettyPrintOptions.DebugIndent | PrettyPrintOptions.MissingNodes);
    }

    void Fill(ItemCollection treeNodes, ReadOnlyCollection<ReflectionStruct> nodes)
    {
      foreach (var node in nodes)
      {
        var treeNode = new TreeViewItem();
        treeNode.Header = node.Description;
        treeNode.Tag = node;
        treeNode.Expanded += node_Expanded;
        if (node.Location.Length == 0)
          treeNode.Background = new SolidColorBrush(Color.FromRgb(200, 255, 200));

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
        treeNode.Expanded -= node_Expanded;
      }
    }

    private void textBox1_TextChanged(object sender, EventArgs e)
    {
      if (_loading)
        return;

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
    
    private void FileOpenExecuted(object sender, RoutedEventArgs e)
    {
      var dialog = new OpenFileDialog { Filter = "C# (.cs)|*.cs|Nitra (.n2)|*.n2|JSON (.json)|*.json|Text (.txt)|*.txt|All|*.*" };
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

    private void DoParse()
    {
      if (_doTreeOperation)
        return;

      if (_currentTestSuit == null)
        return;

      try
      {
        var recovery = _currentTestSuit.Recovery;
        recovery.ReportResult = ReportRecoveryResult;
        _recoveryResults.Clear();
        _recoveryTreeView.Items.Clear();
        _errorsTreeView.Items.Clear();
        var timer = Stopwatch.StartNew();

        _parseResult = _currentTestSuit.Run(_text.Text, null);

        _parseTime.Text = (_parseTimeSpan = timer.Elapsed).ToString();

        _text.TextArea.TextView.Redraw(DispatcherPriority.Input);

        _foldingStrategy.Parser = _parseResult;
        _foldingStrategy.UpdateFoldings(_foldingManager, _text.Document);

        _outliningTime.Text = _foldingStrategy.TimeSpan.ToString();

        _recoveryTime.Text = recovery.Timer.Elapsed.ToString();
        _recoveryCount.Text = recovery.Count.ToString(CultureInfo.InvariantCulture);

        _continueParseTime.Text = recovery.ContinueParseTime.ToString();
        _continueParseCount.Text = recovery.ContinueParseCount.ToString(CultureInfo.InvariantCulture);

        _tryParseSubrulesTime.Text = recovery.TryParseSubrulesTime.ToString();
        _tryParseSubrulesCount.Text = recovery.TryParseSubrulesCount.ToString(CultureInfo.InvariantCulture);

        _tryParseTime.Text = recovery.TryParseTime.ToString();
        _tryParseCount.Text = recovery.TryParseCount.ToString(CultureInfo.InvariantCulture);

        _tryParseNoCacheTime.Text = recovery.TryParseNoCacheTime.ToString();
        _tryParseNoCacheCount.Text = recovery.TryParseNoCacheCount.ToString(CultureInfo.InvariantCulture);

        ShowRecoveryResults();
        TryReportError();
        ShowInfo();
        
        recovery.ReportResult = null;
      }
      catch (TypeLoadException ex)
      {
        ClearMarkers();
        MessageBox.Show(this, ex.Message);
      }
    }

    private void ShowRecoveryResults()
    {
      foreach (var recoveryResult in _recoveryResults)
      {
        var node = new TreeViewItem();
        node.Header = recoveryResult.Item1;
        node.Tag = recoveryResult;
        node.MouseDoubleClick += RecoveryNode_MouseDoubleClick;

        var stackNode = new TreeViewItem();
        stackNode.Header = "Stacks...";
        stackNode.Tag = recoveryResult.Item4;
        stackNode.Expanded += RecoveryNode_Expanded;
        stackNode.Items.Add(new TreeViewItem());
        node.Items.Add(stackNode);

        var bestResultsNode = new TreeViewItem();
        bestResultsNode.Header = "Other best results...";
        bestResultsNode.Tag = recoveryResult.Item2;
        bestResultsNode.Expanded += RecoveryNode_Expanded;
        bestResultsNode.Items.Add(new TreeViewItem());
        node.Items.Add(bestResultsNode);

        var candidatsNode = new TreeViewItem();
        candidatsNode.Header = "All candidats...";
        candidatsNode.Tag = recoveryResult.Item3;
        candidatsNode.Expanded += RecoveryNode_Expanded;
        candidatsNode.Items.Add(new TreeViewItem());
        node.Items.Add(candidatsNode);

        _recoveryTreeView.Items.Add(node);
      }
    }

    void RecoveryNode_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      var recovery = ((RecoveryInfo)((TreeViewItem)e.Source).Tag).Item1;
      _text.Select(recovery.FailPos, recovery.SkipedCount);
      e.Handled = true;
      _text.Focus();
    }

    void RecoveryNode_Expanded(object sender, RoutedEventArgs e)
    {
      var treeNode = (TreeViewItem)e.Source;
      if (treeNode.Items.Count == 1 && ((TreeViewItem)treeNode.Items[0]).Header == null)
      {
        treeNode.Items.Clear();
        var recoveryResults = treeNode.Tag as RecoveryResult[];

        if (recoveryResults != null)
        {
          foreach (var recoveryResult in recoveryResults)
          {
            var node = new TreeViewItem();
            node.Header = recoveryResult;

            foreach (var frame in recoveryResult.Stack)
            {
              var frameNode = new TreeViewItem();
              frameNode.Header = frame;
              node.Items.Add(frameNode);
            }

            treeNode.Items.Add(node);
          }
        }

        var stacks = treeNode.Tag as RecoveryStack[];

        if (stacks != null)
        {
          foreach (var stack in stacks)
          {
            var node = new TreeViewItem();
            node.Header = stack.hd;

            foreach (var frame in stack)
            {
              var frameNode = new TreeViewItem();
              frameNode.Header = frame;
              node.Items.Add(frameNode);
            }

            treeNode.Items.Add(node);
          }
        }
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
      var timer = Stopwatch.StartNew();
      _parseResult.GetSpans(line.Offset, line.EndOffset, spans);
      _highlightingTimeSpan = timer.Elapsed;
      _highlightingTime.Text = _highlightingTimeSpan.ToString();

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

    private void FindNext()
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
    }

    private void FindPrev()
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

    private void _tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      UpdateInfo();
      ShowNodeForCaret();
    }

    public Tuple<string, string>[] GetRowsForColumn(int colIndex)
    {
      var result = new List<Tuple<string, string>>();

      foreach (var g in _performanceGrid.Children.OfType<UIElement>().GroupBy(x => Grid.GetRow(x)).OrderBy(x => x.Key))
      {
        string label = null;
        string value = null;

        foreach (var uiElem in g)
        {
          int col = Grid.GetColumn(uiElem);

          if (col == colIndex)
          {
            var labelElem = uiElem as Label;
            if (labelElem != null)
              label = labelElem.Content.ToString();

            var checkBox = uiElem as CheckBox;
            if (checkBox != null)
              label = checkBox.Content.ToString();
          }

          if (col == colIndex + 1)
          {
            var text = uiElem as TextBlock;
            if (text != null)
              value = text.Text;
          }

        }

        if (label == null && value == null)
          continue;

        Debug.Assert(label != null);
        Debug.Assert(value != null);
        result.Add(Tuple.Create(label, value));
      }

      return result.ToArray();
    }

    private static string MakeStr(string str, int maxLen)
    {
      var padding = maxLen - str.Length + 1;
      return new string(' ', padding) + str;
    }

    private string[][] MakePerfData()
    {
      var len = _performanceGrid.ColumnDefinitions.Count / 2;
      var cols = new Tuple<string, string>[len][];
      var maxLabelLen = new int[len];
      var maxValueLen = new int[len];

      for (int col = 0; col < len; col++)
      {
        var colData = GetRowsForColumn(col * 2);
        cols[col] = colData.ToArray();
        maxLabelLen[col] = colData.Max(x => x.Item1.Length);
        maxValueLen[col] = colData.Max(x => x.Item2.Length);
      }

      string[][] strings = new string[len][];

      for (int col = 0; col < len; col++)
      {
        strings[col] = new string[cols[col].Length];
        for (int row = 0; row < strings[col].Length; row++)
          strings[col][row] = MakeStr(cols[col][row].Item1, maxLabelLen[col]) + MakeStr(cols[col][row].Item2, maxValueLen[col]);
      }

      return strings;
    }

    private void _copyButton_Click(object sender, RoutedEventArgs e)
    {
      var rows = _performanceGrid.RowDefinitions.Count - 1;
      var data = MakePerfData();
      var cols = data.Length;
      var sb = new StringBuilder();

      for (int row = 0; row < rows; row++)
      {
        for (int col = 0; col < cols; col++)
        {
          sb.Append(data[col][row]);
          if (col != cols - 1)
            sb.Append(" │");
        }
        sb.AppendLine();
      }

      var result = sb.ToString();

      Clipboard.SetData(DataFormats.Text, result);
      Clipboard.SetData(DataFormats.UnicodeText, result);
    }

    private void CopyReflectionNodeText(object sender, ExecutedRoutedEventArgs e)
    {
      var value = treeView1.SelectedValue as TreeViewItem;

      if (value != null)
      {
        var result = value.Header.ToString();
        Clipboard.SetData(DataFormats.Text, result);
        Clipboard.SetData(DataFormats.UnicodeText, result);
      }
    }

    private void MenuItem_Click_TestsSettings(object sender, RoutedEventArgs e)
    {
      ShowTestsSettingsDialog();
    }

    bool CheckTestFolder()
    {
      if (Directory.Exists(_settings.TestsLocationRoot ?? ""))
        return true;

      return ShowTestsSettingsDialog();
    }

    private bool ShowTestsSettingsDialog()
    {
      var dialog = new TestsSettingsWindow { Owner = this };
      if (dialog.ShowDialog() ?? false)
      {
        _settings.TestsLocationRoot = dialog.TestsLocationRoot;
        return true;
      }

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
      var testSuits = (ObservableCollection<TestSuitVm>) _testsTreeView.ItemsSource;
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

    private void RunTest(TestVm test)
    {
      test.Run();

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
        // определяем нужно ли выводить разделитель
        var nextIndexA = Math.Max(indexA, diffItem.Index - rangeToShow);
        if (nextIndexA > indexA + 1)
          output.Add(MakeLine("..."));

        // показваем не боле rangeToShow предыдущих строк
        indexA = nextIndexA;
        while (indexA < diffItem.Index)
        {
          output.Add(MakeLine(textA[indexA]));
          ++indexA;
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

        // показываем не более rangeToShow последующих строк
        var tailLinesToShow = Math.Min(rangeToShow, textA.Length - indexA);

        for (var i = 0; i < tailLinesToShow; ++i)
        {
          output.Add(MakeLine(textA[indexA]));
          ++indexA;
        }
      }

      if (indexA < textA.Length)
        output.Add(MakeLine("..."));

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
      if (!CheckTestFolder())
        return;

      var dialog = new TestSuit(true, _currentTestSuit) { Owner = this };
      if (dialog.ShowDialog() ?? false)
        LoadTests();
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
    }

    private void OnRemoveTestSuit(object sender, ExecutedRoutedEventArgs e)
    {
      if (_currentTestSuit == null)
        return;

      if (MessageBox.Show(this, "Do you want to delete the '" + _currentTestSuit.Name + "' test suit?\r\nAll test will be deleted!", "Visualizer!", MessageBoxButton.YesNo,
        MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
        return;

      Directory.Delete(TestFullPath(_currentTestSuit.TestSuitPath), true);
      LoadTests();
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
          MessageBox.Show(this, "Fail to update the test '" + test.Name + "'." + Environment.NewLine + ex.Message, "Visualizer!", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Error);
        }
      }
    }
  }
}
