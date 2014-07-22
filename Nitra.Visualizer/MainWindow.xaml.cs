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
using Nitra.DebugStrategies;
using Nitra.Internal.Recovery;
using Nitra.Runtime.Reflection;
using Nitra.Visualizer.Properties;
using System.Diagnostics;
using System.Text;
using Nitra.Visualizer.ViewModels;
using Nemerle.Diff;

namespace Nitra.Visualizer
{
  using System.Windows.Documents;

  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow
  {
    bool _loading = true;
    ParseResult _parseResult;
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
    Ast _ast;
    TimeSpan _parseTimeSpan;
    TimeSpan _astTimeSpan;
    TimeSpan _highlightingTimeSpan;
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


      _configComboBox.ItemsSource = new[] {"Debug", "Release"};
      var config = _settings.Config;
      _configComboBox.SelectedItem = config == "Release" ? "Release" : "Debug";

      _tabControl.SelectedIndex = _settings.ActiveTabIndex;
      _findGrid.Visibility      = System.Windows.Visibility.Collapsed;
      _foldingStrategy          = new NitraFoldingStrategy();
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
      _text.Options.ConvertTabsToSpaces = true;
      _text.Options.EnableRectangularSelection = true;
      _text.Options.IndentationSize = 2;
      _testsTreeView.SelectedValuePath = "FullPath";

      LoadTests();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      _loading = false;

      if ((Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) != 0)
        return;
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
    }

    private void LoadTests()
    {
      var selected = _testsTreeView.SelectedItem as FullPathVm;
      var path     = selected == null ? null : selected.FullPath;
      var testSuits = new ObservableCollection<TestSuitVm>();

      if (!Directory.Exists(_settings.TestsLocationRoot ?? ""))
        return;

      foreach (var dir in Directory.GetDirectories(_settings.TestsLocationRoot ?? ""))
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


      if (_reflectionTreeView.ItemsSource == null)
        return;

      if (_reflectionTreeView.IsKeyboardFocusWithin)
        return;


      _doChangeCaretPos = true;
      try
      {
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
      finally
      {
        _doChangeCaretPos = false;
      }
    }

    private ReflectionStruct FindNode(IEnumerable<ReflectionStruct> items, int p)
    {
      foreach (ReflectionStruct node in items)
      {
        if (node.Location.StartPos <= p && p < node.Location.EndPos)
        {

          if (node.Children.Count == 0)
            return node;

          _reflectionTreeView.Expand(node);

          return FindNode(node.Children, p);
        }
      }

      return null;
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
      catch(Exception e)
      {
        Debug.Write(e);
      }
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
      if (_parseResult == null)
        return;
      if (IsSplicable(_parseResult))
        return;

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

      //_reflectionTreeView.Items.Clear();

      if (_parseResult == null)
        return;

      var root = _parseResult.Reflect();

      _reflectionTreeView.ItemsSource = new[] { (ReflectionStruct)root };

      //var treeNode = new TreeViewItem();
      //treeNode.Expanded += node_Expanded;
      //treeNode.Header = root.Description;
      //treeNode.Tag = root;
      //treeNode.ContextMenu = (ContextMenu)Resources["TreeContextMenu"];
      //if (root.Children.Count != 0)
      //  treeNode.Items.Add(new TreeViewItem());
      //treeView1.Items.Add(treeNode);
    }

    private void UpdateHtmlPrettyPrint()
    {
      _needUpdateHtmlPrettyPrint = false;

      if (_parseResult == null)
        return;
      if (IsSplicable(_parseResult))
        return;

      if (_ast == null)
        _ast = _parseResult.CreateAst();

      var htmlWriter = new HtmlPrettyPrintWriter(PrettyPrintOptions.DebugIndent | PrettyPrintOptions.MissingNodes, "missing", "debug", "garbage");
      _ast.PrettyPrint(htmlWriter, 0);
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

    private void DoParse()
    {
      if (_doTreeOperation)
        return;

      if (_currentTestSuit == null)
        return;

      try
      {
        var recovery = new RecoveryVisualizer();
        _recoveryTreeView.Items.Clear();
        _errorsTreeView.Items.Clear();
        _reflectionTreeView.ItemsSource = null;
        var timer = Stopwatch.StartNew();

        RecoveryDebug.CurrentTestName = null;

        _parseResult = _currentTestSuit.Run(_text.Text, null, recovery.Strategy);

        _parseTime.Text = (_parseTimeSpan = timer.Elapsed).ToString();

        _text.TextArea.TextView.Redraw(DispatcherPriority.Input);

        _foldingStrategy.ParseResult = _parseResult;
        _foldingStrategy.UpdateFoldings(_foldingManager, _text.Document);

        _outliningTime.Text = _foldingStrategy.TimeSpan.ToString();

        _recoveryTime.Text        = recovery.RecoveryPerformanceData.Timer.Elapsed.ToString();
        _earleyParseTime.Text     = recovery.RecoveryPerformanceData.EarleyParseTime.ToString();
        _recoverAllWaysTime.Text  = recovery.RecoveryPerformanceData.RecoverAllWaysTime.ToString();
        _findBestPathTime.Text    = recovery.RecoveryPerformanceData.FindBestPathTime.ToString();
        _flattenSequenceTime.Text = recovery.RecoveryPerformanceData.FlattenSequenceTime.ToString();
        _parseErrorCount.Text     = recovery.RecoveryPerformanceData.ParseErrorCount.ToString(CultureInfo.InvariantCulture);
        
        TryReportError();
        ShowInfo();
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
      _textMarkerService.RemoveAll(_ => true);
    }

    private void textBox1_HighlightLine(object sender, HighlightLineEventArgs e)
    {
      if (_parseResult == null)
        return;

      try
      {
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
      var text   = _text.Text;
      var index  = _text.SelectionStart + _text.SelectionLength - 1;

      do
      {
        if (index < text.Length)
          index++;
        index = text.IndexOf(toFind, index, option);
      }
      while (index >= 0 && !IsWholeWord(text, index, toFind.Length));

      var found = index >= 0;
      if (found)
      {
        _text.Select(index, toFind.Length);
        _text.ScrollToLine(_text.TextArea.Caret.Line);
      }
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
      {
        _text.Select(index, toFind.Length);
        _text.ScrollToLine(_text.TextArea.Caret.Line);
      }
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
          var currRows = data[col];

          if (row < currRows.Length)
          {
            sb.Append(currRows[row]);
            if (col != cols - 1)
              sb.Append(" │");
          }
        }
        sb.AppendLine();
      }

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

    private void MenuItem_Click_TestsSettings(object sender, RoutedEventArgs e)
    {
      if (ShowTestsSettingsDialog())
        LoadTests();
    }

    bool CheckTestFolder()
    {
      if (Directory.Exists(_settings.TestsLocationRoot ?? ""))
        return true;

      return ShowTestsSettingsDialog();
    }

    private bool ShowTestsSettingsDialog()
    {
      var dialog = new TestsSettingsWindow();
      
      if (this.IsVisible)
        dialog.Owner = this;

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
        RunTests(parseResult => new Recovery().Strategy(parseResult));
      else
        MessageBox.Show(this, "Can't run tests.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void RunTests(RecoveryStrategy recoveryStrategy)
    {
      if (_testsTreeView.ItemsSource == null)
        return;

      var testSuits = (ObservableCollection<TestSuitVm>)_testsTreeView.ItemsSource;

      foreach (var testSuit in testSuits)
      {
        foreach (var test in testSuit.Tests)
          RunTest(test, recoveryStrategy);

        testSuit.TestStateChanged();
      }
    }

    private void RunTest(TestVm test, RecoveryStrategy recoveryStrategy)
    {
      test.Run(recoveryStrategy);

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

      SaveSelectedTestAndTestSuit();

      _settings.Save();
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
      RunTest(parseResult => new Recovery().Strategy(parseResult));
    }

    private void RunTest(RecoveryStrategy recoveryStrategy)
    {
      {
        var test = _testsTreeView.SelectedItem as TestVm;
        if (test != null)
        {
          RunTest(test, recoveryStrategy);
          test.TestSuit.TestStateChanged();

          if (test.TestState == TestState.Failure)
            _testResultDiffTabItem.IsSelected = true;
        }
      }
      var testSuit = _testsTreeView.SelectedItem as TestSuitVm;
      if (testSuit != null)
      {
        foreach (var test in testSuit.Tests)
          RunTest(test, recoveryStrategy);
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
      RunTest(parseResult => new Recovery().Strategy(parseResult));
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

    private void OnRepars(object sender, ExecutedRoutedEventArgs e)
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
        _text.TextArea.Caret.Offset = node.Location.StartPos;
        _text.ScrollTo(_text.TextArea.Caret.Line, _text.TextArea.Caret.Line);
        _text.TextArea.AllowCaretOutsideSelection();
        _text.Select(node.Location.StartPos, node.Location.Length);
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

    private static bool IsSplicable(ParseResult parseResult)
    {
      return parseResult.RuleParser.Descriptor.Grammar.IsSplicable;
    }
    
    private void FindNext_Executed_Selected(object sender, ExecutedRoutedEventArgs e)
    {
      FindSelected(next: true);
    }

    private void FindPrev_Executed_Selected(object sender, ExecutedRoutedEventArgs e)
    {
      FindSelected(next: false);
    }

    private void FindSelected(bool next)
    {
      if (_text.SelectionLength > 0)
      {
        _findText.Text = _text.SelectedText;
        
        if (next)
          FindNext();
        else
          FindPrev();
      }
      else
      {
        var line = _text.Document.Lines[_text.TextArea.Caret.Line - 1];
        var text = _text.Document.GetText(line.Offset, line.Length);
        var startIndex = Math.Min(_text.TextArea.Caret.Column - 1, text.Length - 1);
        var firstCh = text[startIndex];
        int patternStartIndex;
        string searchPattern = IsIdentifier(firstCh)
          ? ExtractIdentifier(text, startIndex, out patternStartIndex)
          : ExtractNotEmpty(text, startIndex, out patternStartIndex);

        if (!string.IsNullOrWhiteSpace(searchPattern))
        {
          _findText.Text = searchPattern;
          _text.Select(line.Offset + patternStartIndex, searchPattern.Length);

          if (next)
            FindNext();
          else
          {
            _text.TextArea.Caret.Column = patternStartIndex + 1;
            FindPrev();
          }
        }
      }
    }

    public static string ExtractNotEmpty(string text, int startIndex, out int patternStartIndex)
    {
      return ExtractString(text, startIndex, char.IsWhiteSpace, out patternStartIndex);
    }

    public static string ExtractIdentifier(string text, int startIndex, out int patternStartIndex)
    {
      return ExtractString(text, startIndex, ch => !IsIdentifier(ch), out patternStartIndex);
    }

    public static string ExtractString(string text, int startIndex, Func<char, bool> predicate, out int patternStartIndex)
    {
      int i = startIndex;
      for (; i > 0; i--)
      {
        var ch = text[i];
        if (predicate(ch))
        {
          i++;
          break;
        }
      }

      int j = startIndex;
      for (; j < text.Length; j++)
      {
        var ch = text[j];
        if (predicate(ch))
          break;
      }

      patternStartIndex = i;
      return text.Substring(i, j - i);
    }

    private static bool IsIdentifier(char ch)
    {
      return char.IsLetterOrDigit(ch) || ch == '_';
    }

    private void _control_KeyDown_resize(object sender, KeyEventArgs e)
    {
      var control = sender as Control;
      if (control != null)
      {
        if (e.Key == Key.Add && Keyboard.Modifiers == ModifierKeys.Control)
          control.FontSize++;
        else if (e.Key == Key.Subtract && Keyboard.Modifiers == ModifierKeys.Control)
          control.FontSize--;
      }
    }
  }
}
