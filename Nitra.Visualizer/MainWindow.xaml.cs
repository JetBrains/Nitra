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
using Nitra.Visualizer.Controls;
using Microsoft.VisualBasic.FileIO;
using Nitra.ProjectSystem;
using Nitra.Runtime.Highlighting;
using Clipboard = System.Windows.Clipboard;
using ContextMenu = System.Windows.Controls.ContextMenu;
using Control = System.Windows.Controls.Control;
using DataFormats = System.Windows.DataFormats;
using File = System.IO.File;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Timer = System.Timers.Timer;
using ToolTip = System.Windows.Controls.ToolTip;

namespace Nitra.Visualizer
{
  using System.Windows.Documents;
  using Interop;
  using System.Windows.Interop;

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
    readonly Timer _nodeForCaretTimer;
    readonly Dictionary<string, HighlightingColor> _highlightingStyles;
    readonly TextMarkerService _textMarkerService;
    readonly NitraFoldingStrategy _foldingStrategy;
    readonly FoldingManager _foldingManager;
    readonly ToolTip _textBox1Tooltip;
    bool _needUpdateReflection;
    bool _needUpdateHtmlPrettyPrint;
    bool _needUpdateTextPrettyPrint;
    ParseTree _parseTree;
    readonly Settings _settings;
    private SolutionVm _solution;
    private TestSuiteVm _currentTestSuite;
    private TestFolderVm _currentTestFolder;
    private TestVm _currentTest;
    private readonly PependentPropertyGrid _propertyGrid;
    private readonly MatchBracketsWalker _matchBracketsWalker = new MatchBracketsWalker();
    private readonly List<ITextMarker> _matchedBracketsMarkers = new List<ITextMarker>();
    private List<MatchBracketsWalker.MatchBrackets> _matchedBrackets;
    private const string ErrorMarkerTag = "Error";

    public MainWindow()
    {
      _settings = Settings.Default;
      _highlightingStyles = new Dictionary<string, HighlightingColor>(StringComparer.OrdinalIgnoreCase);

      ToolTipService.ShowDurationProperty.OverrideMetadata(
        typeof(DependencyObject),
        new FrameworkPropertyMetadata(Int32.MaxValue));

      InitializeComponent();

      _mainRow.Height  = new GridLength(_settings.TabControlHeight);


      _configComboBox.ItemsSource = new[] {"Debug", "Release"};
      var config = _settings.Config;
      _configComboBox.SelectedItem = config == "Release" ? "Release" : "Debug";

      _tabControl.SelectedIndex = _settings.ActiveTabIndex;
      _foldingStrategy          = new NitraFoldingStrategy();
      _textBox1Tooltip          = new ToolTip { PlacementTarget = _text };
      _parseTimer               = new Timer { AutoReset = false, Enabled = false, Interval = 300 };
      _parseTimer.Elapsed       += _parseTimer_Elapsed;
      _nodeForCaretTimer        = new Timer {AutoReset = false, Enabled = false, Interval = 500};
      _nodeForCaretTimer.Elapsed += _nodeForCaretTimer_Elapsed;

      _text.TextArea.Caret.PositionChanged += Caret_PositionChanged;

      _foldingManager    = FoldingManager.Install(_text.TextArea);
      _textMarkerService = new TextMarkerService(_text.Document);

      _text.TextArea.TextView.BackgroundRenderers.Add(_textMarkerService);
      _text.TextArea.TextView.LineTransformers.Add(_textMarkerService);
      _text.Options.ConvertTabsToSpaces = true;
      _text.Options.EnableRectangularSelection = true;
      _text.Options.IndentationSize = 2;
      _testsTreeView.SelectedValuePath = "FullPath";
      _propertyGrid = new PependentPropertyGrid();
      _windowsFormsHost.Child = _propertyGrid;

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
        SelectTest(_settings.SelectedTestSuite, _settings.SelectedTest, _settings.SelectedTestFolder);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
      base.OnSourceInitialized(e);
      this.SetPlacement(_settings.MainWindowPlacement);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      _settings.MainWindowPlacement = this.GetPlacement();
      _settings.Config = (string)_configComboBox.SelectedValue;
      _settings.TabControlHeight = _mainRow.Height.Value;
      _settings.LastTextInput = _text.Text;
      _settings.ActiveTabIndex = _tabControl.SelectedIndex;

      SaveSelectedTestAndTestSuite();
      _settings.Save();
    }

    void SetPlacement(string placementXml)
    {
      var helper = new WindowInteropHelper(this);
      var handle = helper.Handle;
      WindowPlacement.SetPlacement(handle, placementXml);
    }

    string GetPlacement()
    {
      var helper = new WindowInteropHelper(this);
      var handle = helper.Handle;
      return WindowPlacement.GetPlacement(handle);
    }

    private void SaveSelectedTestAndTestSuite()
    {
      if (_currentTestSuite != null)
      {
        _settings.SelectedTestSuite = _currentTestSuite.TestSuitePath;
        var test = _testsTreeView.SelectedItem as TestVm;
        _settings.SelectedTest = test == null ? null : test.Name;
        var testFolder = test == null ? null : test.Parent as TestFolderVm;
        _settings.SelectedTestFolder = testFolder == null ? null : testFolder.Name;
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

      _solution = new SolutionVm(_settings.CurrentSolution, selectedPath, _settings.Config);
      this.Title = _solution.Name + " - " + Constants.AppName;
      _testsTreeView.ItemsSource = _solution.TestSuites;
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

      _nodeForCaretTimer.Stop();
      _nodeForCaretTimer.Start();
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
    }

    private TreeViewItem FindNode(TreeViewItem item, int pos, List<NSpan> checkedSpans = null)
    {
      checkedSpans = checkedSpans ?? new List<NSpan>();
      var ast = item.Tag as IAst;

      if (ast == null)
        return null;

      // check for circular dependency
      for (var i = 0; i < checkedSpans.Count; i++) { 
        // if current span was previously checked
        if (ast.Span == checkedSpans[i]) {
          // and it's not a topmost span
          for (var k = i; k < checkedSpans.Count; k++)
            if (ast.Span != checkedSpans[k])
              // Stop FindNode recursion
              return item;
          break;
        }
      }
      
      checkedSpans.Add(ast.Span);

      if (ast.Span.IntersectsWith(pos))
      {
        item.IsExpanded = true;
        foreach (TreeViewItem subItem in item.Items)
        {
          var result = FindNode(subItem, pos, checkedSpans);
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

    private void TryReportError()
    {
      if (_parseResult == null)
        if (_currentTestSuite.Exception != null)
        {
          var msg = "Exception: " + _currentTestSuite.Exception.Message;
          _status.Text = msg;

          var errorNode = new TreeViewItem();
          errorNode.Header = "(1,1): " + msg;
          errorNode.Tag = _currentTestSuite.Exception;
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
        var cmpilerMessages = new List<CompilerMessage>();
        var errorNodes = _errorsTreeView.Items;
        var currFile = _currentTest.File;

        if (_currentTestFolder != null)
          foreach (var test in _currentTestFolder.Tests)
            cmpilerMessages.AddRange(test.File.GetCompilerMessages());
        else
          cmpilerMessages.AddRange(_currentTest.File.GetCompilerMessages());
         
        cmpilerMessages.Sort();


        foreach (var message in cmpilerMessages)
        {
          var text = message.Text;
          var location = message.Location;
          var file = location.Source.File;
          if (currFile == file)
          {
            var marker = _textMarkerService.Create(location.StartPos, location.Length);
            marker.Tag = ErrorMarkerTag;
            marker.MarkerType = TextMarkerType.SquigglyUnderline;
            marker.MarkerColor = Colors.Red;
            marker.ToolTip = text;
          }

          var errorNode = new TreeViewItem();
          errorNode.Header = Path.GetFileNameWithoutExtension(file.FullName) + "(" + message.Location.StartLineColumn + "): " + text;
          errorNode.Tag = message;
          errorNode.MouseDoubleClick += errorNode_MouseDoubleClick;

          foreach (var nestedMessage in message.NestedMessages)
          {
            var nestadErrorNode = new TreeViewItem();
            nestadErrorNode.Header = Path.GetFileNameWithoutExtension(file.FullName) + "(" + nestedMessage.Location.StartLineColumn + "): " + nestedMessage.Text;
            nestadErrorNode.Tag = nestedMessage;
            nestadErrorNode.MouseDoubleClick += errorNode_MouseDoubleClick;
            errorNode.Items.Add(nestadErrorNode);
          }

          errorNodes.Add(errorNode);
        }

        _status.Text = cmpilerMessages.Count == 0 ? "OK" : cmpilerMessages.Count + " error[s]";
      }
    }

    void errorNode_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      var node = (TreeViewItem)sender;
      if (!node.IsSelected)
        return;
      var error = (CompilerMessage)node.Tag;
      SelectText(error.Location);
      e.Handled = true;
      _text.Focus();
    }

    void ShowInfo()
    {
      _needUpdateReflection      = true;
      _needUpdateHtmlPrettyPrint = true;
      _needUpdateTextPrettyPrint = true;
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
        
        UpdateDeclarations();
      }
      catch(Exception e)
      {
        Debug.Write(e);
      }
    }

    private void UpdateReflection()
    {
      _needUpdateReflection = false;

      if (_parseResult == null)
        return;

      var root = _parseResult.Reflect();
      _reflectionTreeView.ItemsSource = new[] { root };
    }

    private void UpdateHtmlPrettyPrint()
    {
      _needUpdateHtmlPrettyPrint = false;

      if (_parseResult == null)
        return;

      if (_parseTree == null)
        _parseTree = _parseResult.CreateParseTree();

      var htmlWriter = new HtmlPrettyPrintWriter(PrettyPrintOptions.DebugIndent | PrettyPrintOptions.MissingNodes, "missing", "debug", "garbage");
      _parseTree.PrettyPrint(htmlWriter, 0, null);

      var spanStyles = new StringBuilder();
      foreach (var style in _highlightingStyles)
      {
        var brush = style.Value.Foreground as SimpleHighlightingBrush;
        if (brush == null)
          continue;
        var color = brush.Brush.Color;
        spanStyles.Append('.').Append(style.Key.Replace('.', '-')).Append("{color:rgb(").Append(color.R).Append(',').Append(color.G).Append(',').Append(color.B).AppendLine(");}");
      }
      var html = Properties.Resources.PrettyPrintDoughnut.Replace("{spanclasses}", spanStyles.ToString()).Replace("{prettyprint}", htmlWriter.ToString());
      prettyPrintViewer.NavigateToString(html);
    }

    private void UpdateTextPrettyPrint()
    {
      _needUpdateTextPrettyPrint = false;

      if (_parseResult == null)
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

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
      this.Close();
    }

    void _parseTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
      Reparse();
    }

    private void _nodeForCaretTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
      Dispatcher.Invoke(new Action(ShowNodeForCaret));
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

      if (_currentTestSuite == null || _currentTest == null)
        return;

      try
      {
        ClearAll();

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

    private void ClearAll()
    {
      ClearMarkers();
      _parseResult = null;
      _declarationsTreeView.Items.Clear();
      _matchedBracketsMarkers.Clear();
      _recoveryTreeView.Items.Clear();
      _errorsTreeView.Items.Clear();
      _reflectionTreeView.ItemsSource = null;
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
          parseTree.Accept(this);
      }

      public void Visit(Name name)
      {
        var span = name.Span;

        if (!span.IntersectsWith(_span) || !name.IsSymbolEvaluated)
          return;

        var sym = name.Symbol;
        var spanClass = sym.SpanClass;

        if (spanClass == Nitra.Language.DefaultSpanClass)
          return;

        SpanInfos.Add(new SpanInfo(span, spanClass));
      }

      public void Visit(Reference reference)
      {
        var span = reference.Span;

        if (!span.IntersectsWith(_span) || !reference.IsRefEvaluated)
          return;

        IRef r = reference.Ref;
        while (r.IsResolvedToEvaluated)
          r = r.ResolvedTo;

        var spanClass = r.SpanClass;

        if (spanClass == Nitra.Language.DefaultSpanClass)
          return;

        SpanInfos.Add(new SpanInfo(span, spanClass));
      }

      public void Visit(IRef r)
      {
      }
    }

    private void textBox1_HighlightLine(object sender, HighlightLineEventArgs e)
    {
      if (_parseResult == null)
        return;

      try
      {
        var line = e.Line;
        var spans = new HashSet<SpanInfo>();
        _parseResult.GetSpans(line.Offset, line.EndOffset, spans);
        var astRoot = _astRoot;
        if (astRoot != null)
        {
          var visitor = new CollectSymbolsAstVisitor(new NSpan(line.Offset, line.EndOffset));
          astRoot.Accept(visitor);
          foreach (var spanInfo in visitor.SpanInfos)
            spans.Add(spanInfo);
        }

        foreach (var span in spans)
        {
          HighlightingColor color;
          if (!_highlightingStyles.TryGetValue(span.SpanClass.FullName, out color))
          {
            color = MakeHighlightingColor(span.SpanClass);
            _highlightingStyles.Add(span.SpanClass.FullName, color);
          }
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

    private void _tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      UpdateInfo();
      ShowNodeForCaret();
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
      e.CanExecute = _currentTestSuite != null;
      e.Handled = true;
    }

    private void AddTest()
    {
      if (_currentTestSuite == null)
      {
        MessageBox.Show(this, "Select a test suite first.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

      if (_needUpdateTextPrettyPrint)
        UpdateTextPrettyPrint();
      var testSuitePath = _currentTestSuite.TestSuitePath;
      var selectedTestFolder = _currentTestFolder == null ? null : _currentTestFolder.Name;
      var dialog = new AddTest(TestFullPath(testSuitePath), _text.Text, _prettyPrintTextBox.Text) { Owner = this };
      if (dialog.ShowDialog() ?? false)
      {
        var testName = dialog.TestName;
        LoadTests();
        SelectTest(testSuitePath, testName, selectedTestFolder);
      }
    }

    private void SelectTest(string testSuitePath, string testName, string selectedTestFolder)
    {
      if (!CheckTestFolder())
      {
        MessageBox.Show(this, "The test folder does not exist.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

      var testSuites = (ObservableCollection<TestSuiteVm>) _testsTreeView.ItemsSource;

      if (testSuites == null)
        return;

      var result = (ITestTreeContainerNode)testSuites.FirstOrDefault(ts => ts.FullPath == testSuitePath);
      if (result == null)
        return;
      if (selectedTestFolder != null)
      {
        var testFolder = result.Children.FirstOrDefault(t => t.Name == selectedTestFolder);
        if (testFolder != null)
          result = (ITestTreeContainerNode)testFolder;
      }
      var test = result.Children.FirstOrDefault(t => t.Name == testName);
      if (test != null)
      {
        test.IsSelected = true;
      }
      else
      {
        result.IsSelected = true;
      }
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

      var testSuites = (ObservableCollection<TestSuiteVm>)_testsTreeView.ItemsSource;

      foreach (var testSuite in testSuites)
      {
        foreach (var test in testSuite.Tests)
          RunTest(test);

        testSuite.TestStateChanged();
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

    private void OnAddTestSuite(object sender, ExecutedRoutedEventArgs e)
    {
      EditTestSuite(true);
    }

    private void EditTestSuite(bool create)
    {
      if (_solution == null)
        return;
      var currentTestSuite = _currentTestSuite;
      var dialog = new TestSuiteDialog(create, currentTestSuite, _settings) { Owner = this };
      if (dialog.ShowDialog() ?? false)
      {
        if (currentTestSuite != null)
          _solution.TestSuites.Remove(currentTestSuite);
        var testSuite = new TestSuiteVm(_solution, dialog.TestSuiteName, _settings.Config);
        testSuite.IsSelected = true;
        _solution.Save();
      }
    }


    private void OnRemoveTest(object sender, ExecutedRoutedEventArgs e)
    {
      var test = _testsTreeView.SelectedItem as ITest;
      if (test == null)
        return;
      if (MessageBox.Show(this, "Do you want to delete the '" + test.Name + "' test?", "Visualizer!", MessageBoxButton.YesNo,
        MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
        return;

      Delete();
    }

    private void CommandBinding_CanRemoveTest(object sender, CanExecuteRoutedEventArgs e)
    {
      if (_testsTreeView == null)
        return;
      e.CanExecute = _testsTreeView.SelectedItem is TestVm || _testsTreeView.SelectedItem is TestFolderVm;
      e.Handled = true;
    }

    private void _testsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
      _loading = true;
      try
      {
        var test = e.NewValue as TestVm;
        if (test != null)
        {
          _parseResult = null;
          ChangeCurrentTest(test.TestSuite, test.Parent as TestFolderVm, test, test.Code);
          ShowDiff(test);
        }

        var testFolder = e.NewValue as TestFolderVm;
        if (testFolder != null)
        {
          ClearAll();
          ChangeCurrentTest(testFolder.TestSuite, testFolder, null, "");
        }

        var testSuite = e.NewValue as TestSuiteVm;
        if (testSuite != null)
        {
          ClearAll();
          ChangeCurrentTest(testSuite, null, null, "");
          _para.Inlines.Clear();
        }
      }
      finally
      {
        _loading = false;
      }
      SaveSelectedTestAndTestSuite();
      _settings.Save();
      Reparse();
    }

    private void ChangeCurrentTest(TestSuiteVm newTestSuite, TestFolderVm newTestFolder, TestVm newTest, string code)
    {
      if (newTestSuite != _currentTestSuite && newTestSuite != null)
      {
        _highlightingStyles.Clear();
        foreach (var spanClass in newTestSuite.Language.GetSpanClasses())
          _highlightingStyles.Add(spanClass.FullName, MakeHighlightingColor(spanClass));
      }
      _currentTestSuite = newTestSuite;
      _currentTestFolder = newTestFolder;
      _currentTest = newTest;
      _text.Text = code;
    }

    private HighlightingColor MakeHighlightingColor(SpanClass spanClass)
    {
      return new HighlightingColor
      {
        Foreground = new SimpleHighlightingBrush(ColorFromArgb(spanClass.Style.ForegroundColor))
      };
    }

    private void OnRemoveTestSuite(object sender, ExecutedRoutedEventArgs e)
    {
      if (_solution == null || _currentTestSuite == null)
        return;

      if (MessageBox.Show(this, "Do you want to delete the '" + _currentTestSuite.Name + "' test suite?\r\nAll test will be deleted!", "Visualizer!", MessageBoxButton.YesNo,
        MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
        return;

      _currentTestSuite.Remove();
    }

    private void CommandBinding_CanRemoveTestSuite(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = _currentTestSuite != null;
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
          test.TestSuite.TestStateChanged();

          if (test.TestState == TestState.Failure)
            _testResultDiffTabItem.IsSelected = true;
        }
      }
      var testSuite = _testsTreeView.SelectedItem as TestSuiteVm;
      if (testSuite != null)
      {
        foreach (var test in testSuite.Tests)
          RunTest(test);
        testSuite.TestStateChanged();
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
      else if (_testsTreeView.SelectedItem is TestSuiteVm)
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
          test.TestSuite.TestStateChanged();
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
      Reparse();
    }

    private void Reparse()
    {
      if (Dispatcher.CheckAccess())
        DoParse();
      else
        Dispatcher.Invoke(new Action(DoParse));
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
        _text.ScrollTo(_text.TextArea.Caret.Line, _text.TextArea.Caret.Column);
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
      if (_currentTestSuite == null)
        return;

      _currentTestSuite.ShowGrammar();
    }

    private void CommandBinding_CanShowGrammar(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = _currentTestSuite != null;
      e.Handled = true;
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
          ShowCompletionWindow(_text.CaretOffset);
          e.Handled = true;
        }
        else if (e.Key == Key.Oem6) // Oem6 - '}'
          TryMatchBraces();
      }
    }

    private void ShowCompletionWindow(int pos)
    {
      if(_parseResult == null || _astRoot == null)
        return;

      var completionList = CompleteWord(pos, _astRoot);

      _completionWindow = new CompletionWindow(_text.TextArea);
      IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;

      foreach (var completionData in completionList)
        if (!string.IsNullOrEmpty(completionData.Text) && char.IsLetter(completionData.Text[0]))
          data.Add(completionData);

      _completionWindow.Show();
      _completionWindow.Closed += delegate { _completionWindow = null; };
    }

    private List<CompletionData> CompleteWord(int pos, IAst astRoot)
    {
      NSpan replacementSpan;
      var parseResult = astRoot.File.ParseResult;
      var result = NitraUtils.CompleteWord(pos, parseResult, astRoot, out replacementSpan);
      var completionList = new List<CompletionData>();

      foreach (var elem in result)
      {
        var symbol = elem as DeclarationSymbol;
        if (symbol != null && symbol.IsNameValid)
        {
          var content = symbol.ToXaml();
          var description = content;
          // TODO: починить отображение неоднозначностей
          //var amb = symbol as IAmbiguousSymbol;
          //if (amb != null)
          //  description = Utils.WrapToXaml(string.Join(@"<LineBreak/>", amb.Ambiguous.Select(a => a.ToXaml())));
          completionList.Add(new CompletionData(replacementSpan, symbol.Name, content, description, priority: 1.0));
        }

        var literal = elem as string;
        if (literal != null)
        {
          var escaped = Utils.Escape(literal);
          var xaml = "<Span Foreground='blue'>" + escaped + "</Span>";
          completionList.Add(new CompletionData(replacementSpan, literal, xaml, "keyword " + xaml, priority: 2.0));
        }
      }

      return completionList;
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
      _text.ScrollTo(_text.TextArea.Caret.Line, _text.TextArea.Caret.Column);
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
      _solution = new SolutionVm(solutionFilePath, null, _settings.Config);
      _testsTreeView.ItemsSource = _solution.TestSuites;
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

    private void OnAddExistsTestSuite(object sender, ExecutedRoutedEventArgs e)
    {
      var unattachedTestSuites = _solution.GetUnattachedTestSuites();
      var menu = new ContextMenu();
      foreach (var name in unattachedTestSuites)
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
      var name = (string)((MenuItem)e.Source).Header;
      var testSuite = new TestSuiteVm(_solution, name, _settings.Config);
      testSuite.IsSelected = true;
      _solution.Save();
    }

    private void CommandBinding_CanOnAddTestSuite(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = _solution != null;
      e.Handled = true;
    }

    private void OnEditTestSuite(object sender, ExecutedRoutedEventArgs e)
    {
      EditTestSuite(false);
    }

    private void CommandBinding_CanOnEditTestSuite(object sender, CanExecuteRoutedEventArgs e)
    {
      e.Handled = true;
      e.CanExecute = _currentTestSuite != null;
    }

    private void RecentFileList_OnMenuClick(object sender, RecentFileList.MenuClickEventArgs e)
    {
      OpenSolution(e.Filepath);
    }

    private void OnUsePanicRecovery(object sender, ExecutedRoutedEventArgs e)
    {
      OnReparse(null, null);
    }

    private void EventSetter_OnHandler(object sender, MouseButtonEventArgs e)
    {
      var tvi = sender as TreeViewItem;

      if (tvi != null && e.ChangedButton == MouseButton.Right && e.ButtonState == MouseButtonState.Pressed)
        tvi.IsSelected = true;
    }

    private static string MakeTestFileName(TestFolderVm testFolder)
    {
      var names = new bool['Z' - 'A'];
      foreach (var t in testFolder.Tests)
      {
        var name = t.Name;
        if (name.Length == 1)
        {
          var ch = name[0];
          if (ch >= 'A' && ch <= 'Z')
            names[ch - 'A'] = true;
        }
      }

      for (int i = 0; i < names.Length; i++)
        if (!names[i])
          return ((char)('A' + i)).ToString();

      return Path.GetFileNameWithoutExtension(Path.GetTempFileName());
    }

    private void AddFile_MenuItem_OnClick(object sender, RoutedEventArgs e)
    {
      TestFolderVm testFolder;
      var test = _testsTreeView.SelectedItem as TestVm;
      if (test != null)
      {
        var dirPath = Path.ChangeExtension(test.TestPath, null);
        if (!Directory.Exists(dirPath))
          Directory.CreateDirectory(dirPath);

        testFolder = new TestFolderVm(dirPath, test.TestSuite);
        var suite = (TestSuiteVm)test.Parent;
        var index = suite.Tests.IndexOf(test);
        suite.Tests[index] = testFolder;

        var firstFilePath = Path.Combine(dirPath, MakeTestFileName(testFolder) + ".test");
        if (File.Exists(test.TestPath))
          File.Move(test.TestPath, firstFilePath);
        else
          File.WriteAllText(firstFilePath, Environment.NewLine, Encoding.UTF8);

        if (File.Exists(test.GolgPath))
          File.Move(test.GolgPath, Path.ChangeExtension(firstFilePath, ".gold"));

        testFolder.Tests.Add(new TestVm(firstFilePath, testFolder));

        AddNewFileToMultitest(testFolder).IsSelected = true;
        return;
      }

      testFolder = _testsTreeView.SelectedItem as TestFolderVm;
      if (testFolder != null)
      {
        AddNewFileToMultitest(testFolder).IsSelected = true;
      }
    }

    private static TestVm AddNewFileToMultitest(TestFolderVm testFolder)
    {
      var name = MakeTestFileName(testFolder);
      var path = Path.Combine(testFolder.TestPath, name + ".test");
      File.WriteAllText(path, Environment.NewLine, Encoding.UTF8);
      var newTest = new TestVm(path, testFolder);
      testFolder.Tests.Add(newTest);
      return newTest;
    }

    private void CopyReflectionText(object sender, RoutedEventArgs e)
    {
      var reflectionStruct = _reflectionTreeView.SelectedItem as ReflectionStruct;
      if (reflectionStruct != null)
        CopyTreeNodeToClipboard(reflectionStruct.Description);
    }

    private void DeleteFile_MenuItem_OnClick(object sender, RoutedEventArgs e)
    {
      Delete();
    }

    private void Delete()
    {
      TestFolderVm testFolder;
      var test = _testsTreeView.SelectedItem as TestVm;
      if (test != null)
      {
        if (File.Exists(test.TestPath))
          File.Delete(test.TestPath);
        var goldPath = Path.ChangeExtension(test.TestPath, ".gold");
        if (File.Exists(goldPath))
          File.Delete(goldPath);
        testFolder = test.Parent as TestFolderVm;
        if (testFolder != null)
        {
          var index = testFolder.Tests.IndexOf(test);
          testFolder.Tests.Remove(test);
          if (index < testFolder.Tests.Count)
            testFolder.Tests[index].IsSelected = true;
          else if (index > 0)
            testFolder.Tests[index - 1].IsSelected = true;
        }
        else
        {
          var suite = test.TestSuite;
          var index = suite.Tests.IndexOf(test);
          test.TestSuite.Tests.Remove(test);
          if (index < suite.Tests.Count)
            suite.Tests[index].IsSelected = true;
          else if (index > 0)
            suite.Tests[index - 1].IsSelected = true;
        }

        return;
      }
      testFolder = _testsTreeView.SelectedItem as TestFolderVm;
      if (testFolder != null)
      {
        if (Directory.Exists(testFolder.TestPath))
          FileSystem.DeleteDirectory(testFolder.TestPath, UIOption.OnlyErrorDialogs, RecycleOption.DeletePermanently);
        var suite = testFolder.TestSuite;
        var index = suite.Tests.IndexOf(testFolder);
        suite.Tests.Remove(testFolder);
        if (index < suite.Tests.Count)
          suite.Tests[index].IsSelected = true;
        else if (index > 0)
          suite.Tests[index - 1].IsSelected = true;
      }
    }

    public Color ColorFromArgb(int argb)
    {
      unchecked
      {
        return Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
      }
    }
  }
}
