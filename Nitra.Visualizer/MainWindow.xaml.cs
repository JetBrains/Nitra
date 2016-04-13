using Common;

using ICSharpCode.AvalonEdit.AddIn;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.SharpDevelop.Editor;

using Microsoft.VisualBasic.FileIO;

using Nemerle.Diff;

using Nitra.ClientServer.Client;
using Nitra.ViewModels;
using Nitra.Visualizer.Controls;
using Nitra.Visualizer.Properties;

using System;
using System.Collections.Generic;
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
  using ClientServer.Messages;
  using Interop;
  using System.Collections.Immutable;
  using System.Windows.Documents;
  using System.Windows.Interop;

  public partial class MainWindow
  {
    bool _initializing = true;
    bool _doTreeOperation;
    bool _doChangeCaretPos;
    readonly Timer _nodeForCaretTimer;
    readonly TextMarkerService _textMarkerService;
    readonly NitraFoldingStrategy _foldingStrategy;
    readonly FoldingManager _foldingManager;
    readonly ToolTip _textBox1Tooltip;
    bool _needUpdateReflection;
    bool _needUpdateHtmlPrettyPrint;
    bool _needUpdateTextPrettyPrint;
    //ParseTree _parseTree;
    readonly Settings _settings;
    WorkspaceVm _workspace;
    SuiteVm _currentSuite;
    ProjectVm _currentProject;
    private SolutionVm _currentSolution;
    TestVm _currentTest;
    readonly PependentPropertyGrid _propertyGrid;
    //readonly MatchBracketsWalker _matchBracketsWalker = new MatchBracketsWalker();
    readonly List<ITextMarker> _matchedBracketsMarkers = new List<ITextMarker>();
    readonly Action<ServerMessage> _responseDispatcher;
    //List<MatchBracketsWalker.MatchBrackets> _matchedBrackets;
    const string ErrorMarkerTag = "Error";

    public MainWindow()
    {
      _settings = Settings.Default;

      ToolTipService.ShowDurationProperty.OverrideMetadata(
        typeof(DependencyObject),
        new FrameworkPropertyMetadata(Int32.MaxValue));

      InitializeComponent();

      _responseDispatcher = msg => _text.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<ServerMessage>(Response), msg);

      _mainRow.Height  = new GridLength(_settings.TabControlHeight);


      _configComboBox.ItemsSource = new[] {"Debug", "Release"};
      var config = _settings.Config;
      _configComboBox.SelectedItem = config == "Release" ? "Release" : "Debug";

      _tabControl.SelectedIndex = _settings.ActiveTabIndex;
      _foldingStrategy          = new NitraFoldingStrategy();
      _textBox1Tooltip          = new ToolTip { PlacementTarget = _text };
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

      if (string.IsNullOrWhiteSpace(_settings.CurrentWorkspace))
        _workspace = null;
      else
        LoadTests();

      _text.Document.Changed += Document_Changed;
      _text.Document.UpdateStarted += DocumentOnUpdateStarted;
      _text.Document.UpdateFinished += DocumentOnUpdateFinished;
    }

    private void DocumentOnUpdateFinished(object sender, EventArgs eventArgs)
    {
      if (_currentTest == null || _initializing)
        return;

      _currentTest.StartBatchCodeUpdate();
    }

    private void DocumentOnUpdateStarted(object sender, EventArgs eventArgs)
    {
      if (_currentTest == null || _initializing)
        return;

      _currentTest.FinishBatchCodeUpdate();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      _initializing = false;

      if ((Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) != 0)
        return;
      if (_workspace != null)
        SelectTest(_settings.SelectedTestNode);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
      base.OnSourceInitialized(e);
      this.SetPlacement(_settings.MainWindowPlacement);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      _initializing = true;

      _settings.MainWindowPlacement = this.GetPlacement();
      _settings.Config = (string)_configComboBox.SelectedValue;
      _settings.TabControlHeight = _mainRow.Height.Value;
      _settings.LastTextInput = _text.Text;
      _settings.ActiveTabIndex = _tabControl.SelectedIndex;
      _settings.Save();
      
      _currentSuite    = null;
      _currentSolution = null;
      _currentProject  = null;
      _currentTest     = null;

      if (_workspace != null)
      {
        foreach (var testSuite in _workspace.TestSuites)
          testSuite.Dispose();
      }
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
      if (_currentTest != null)
        _settings.SelectedTestNode = _currentTest.FullPath;
      else if (_currentProject != null)
        _settings.SelectedTestNode = _currentProject.FullPath;
      else if (_currentSolution != null)
        _settings.SelectedTestNode = _currentSolution.FullPath;
      else if (_currentSuite != null)
        _settings.SelectedTestNode = _currentSuite.FullPath;

      _settings.Save();

      if (_workspace != null && _workspace.IsDirty)
        _workspace.Save();
    }

    private void LoadTests()
    {
      var selected = _testsTreeView.SelectedItem as BaseVm;
      var selectedPath = selected == null ? null : selected.FullPath;

      if (!File.Exists(_settings.CurrentWorkspace ?? ""))
      {
        MessageBox.Show(this, "Workspace '" + _settings.CurrentWorkspace + "' not exists!");
        return;
      }

      _workspace = new WorkspaceVm(_settings.CurrentWorkspace, selectedPath, _settings.Config);
      this.Title = _workspace.Name + " - " + Constants.AppName;
      _testsTreeView.ItemsSource = _workspace.TestSuites;
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
      //if (_matchedBracketsMarkers.Count > 0)
      //{
      //  foreach (var marker in _matchedBracketsMarkers)
      //    _textMarkerService.Remove(marker);
      //  _matchedBracketsMarkers.Clear();
      //}

      //var context = new MatchBracketsWalker.Context(caretPos);
      //_matchBracketsWalker.Walk(_parseResult, context);
      //_matchedBrackets = context.Brackets;

      //if (context.Brackets != null)
      //{
      //  foreach (var bracket in context.Brackets)
      //  {
      //    var marker1 = _textMarkerService.Create(bracket.OpenBracket.StartPos, bracket.OpenBracket.Length);
      //    marker1.BackgroundColor = Colors.LightGray;
      //    _matchedBracketsMarkers.Add(marker1);

      //    var marker2 = _textMarkerService.Create(bracket.CloseBracket.StartPos, bracket.CloseBracket.Length);
      //    marker2.BackgroundColor = Colors.LightGray;
      //    _matchedBracketsMarkers.Add(marker2);
      //  }
      //}
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
      //checkedSpans = checkedSpans ?? new List<NSpan>();
      //var ast = item.Tag as IAst;

      //if (ast == null)
      //  return null;

      //// check for circular dependency
      //for (var i = 0; i < checkedSpans.Count; i++) { 
      //  // if current span was previously checked
      //  if (ast.Span == checkedSpans[i]) {
      //    // and it's not a topmost span
      //    for (var k = i; k < checkedSpans.Count; k++)
      //      if (ast.Span != checkedSpans[k])
      //        // Stop FindNode recursion
      //        return item;
      //    break;
      //  }
      //}
      
      //checkedSpans.Add(ast.Span);

      //if (ast.Span.IntersectsWith(pos))
      //{
      //  item.IsExpanded = true;
      //  foreach (TreeViewItem subItem in item.Items)
      //  {
      //    var result = FindNode(subItem, pos, checkedSpans);
      //    if (result != null)
      //      return result;
      //  }

      //  return item;
      //}

      return null;
    }

    private void ShowParseTreeNodeForCaret()
    {
      //if (_reflectionTreeView.ItemsSource == null)
      //  return;
      //
      //if (_reflectionTreeView.IsKeyboardFocusWithin)
      //  return;
      //
      //
      //var node = FindNode((ReflectionStruct[])_reflectionTreeView.ItemsSource, _text.CaretOffset);
      //
      //if (node != null)
      //{
      //  var selected = _reflectionTreeView.SelectedItem as ReflectionStruct;
      //
      //  if (node == selected)
      //    return;
      //
      //  _reflectionTreeView.SelectedItem = node;
      //  _reflectionTreeView.BringIntoView(node);
      //}
    }

    private ReflectionStruct FindNode(IEnumerable<ReflectionStruct> items, int p)
    {
      //foreach (ReflectionStruct node in items)
      //{
      //  if (node.Span.StartPos <= p && p < node.Span.EndPos) // IntersectsWith(p) includes EndPos
      //  {
      //    if (node.Children.Length == 0)
      //      return node;
      //
      //    _reflectionTreeView.Expand(node);
      //
      //    return FindNode(node.Children, p);
      //  }
      //}

      return null;
    }

    private void TryReportError()
    {
      if (_currentTest == null)
        return;

      var cmpilerMessages = new List<CompilerMessage>();
      cmpilerMessages.AddRange(_currentTest.ParsingMessages);
      cmpilerMessages.AddRange(_currentTest.SemanticAnalysisMessages);
      cmpilerMessages.Sort();

      ClearMarkers();

      var errorNodes      = _errorsTreeView.Items;
      var currentFileId   = _currentTest.Id;
      var fullName        = _currentTest.FullPath;
      var doc             = _text.Document;

      errorNodes.Clear();

      foreach (var message in cmpilerMessages)
      {
        var text     = message.Text;
        var location = message.Location;
        var file     = location.File;
        var span     = location.Span;
        if (currentFileId == file.FileId)
        {
          var marker = _textMarkerService.Create(span.StartPos, span.Length);
          marker.Tag         = ErrorMarkerTag;
          marker.MarkerType  = TextMarkerType.SquigglyUnderline;
          marker.MarkerColor = Colors.Red;
          marker.ToolTip     = text;
        }

        var errorNode = new TreeViewItem();
        var pos = doc.GetLocation(span.StartPos);
        errorNode.Header = Path.GetFileNameWithoutExtension(fullName) + "(" + pos.Line + "," + pos.Column  + "): " + text;
        errorNode.Tag = message;
        errorNode.MouseDoubleClick += errorNode_MouseDoubleClick;

        foreach (var nestedMessage in message.NestedMessages)
        {
          var nestedPos = doc.GetLocation(span.StartPos);
          var nestadErrorNode = new TreeViewItem();
          nestadErrorNode.Header = Path.GetFileNameWithoutExtension(fullName) + "(" + nestedPos.Line + "," + nestedPos.Column + "): " + nestedMessage.Text;
          nestadErrorNode.Tag = nestedMessage;
          nestadErrorNode.MouseDoubleClick += errorNode_MouseDoubleClick;
          errorNode.Items.Add(nestadErrorNode);
        }

        errorNodes.Add(errorNode);
      }

      _status.Text = cmpilerMessages.Count == 0 ? "OK" : cmpilerMessages.Count + " error[s]";
    }

    private void TryReportError2()
    {
      //if (_parseResult == null)
      //  if (_currentSuite.Exception != null)
      //  {
      //    var msg = "Exception: " + _currentSuite.Exception.Message;
      //    _status.Text = msg;

      //    var errorNode = new TreeViewItem();
      //    errorNode.Header = "(1,1): " + msg;
      //    errorNode.Tag = _currentSuite.Exception;
      //    errorNode.MouseDoubleClick += errorNode_MouseDoubleClick;
      //    _errorsTreeView.Items.Add(errorNode);

      //    var marker = _textMarkerService.Create(0, _text.Text.Length);
      //    marker.Tag = ErrorMarkerTag;
      //    marker.MarkerType = TextMarkerType.SquigglyUnderline;
      //    marker.MarkerColor = Colors.Purple;
      //    marker.ToolTip = msg;
      //  }
      //  else
      //    _status.Text = "Not parsed!";
      //else
      //{
      //  var cmpilerMessages = new List<CompilerMessage>();
      //  var errorNodes = _errorsTreeView.Items;
      //  var currFile = _currentTest.File;

      //  if (_currentProject != null)
      //    foreach (var test in _currentProject.Tests)
      //      cmpilerMessages.AddRange(test.File.GetCompilerMessages());
      //  else
      //    cmpilerMessages.AddRange(_currentTest.File.GetCompilerMessages());
         
      //  cmpilerMessages.Sort();


      //  foreach (var message in cmpilerMessages)
      //  {
      //    var text = message.Text;
      //    var location = message.Location;
      //    var file = location.Source.File;
      //    if (currFile == file)
      //    {
      //      var marker = _textMarkerService.Create(location.StartPos, location.Length);
      //      marker.Tag = ErrorMarkerTag;
      //      marker.MarkerType = TextMarkerType.SquigglyUnderline;
      //      marker.MarkerColor = Colors.Red;
      //      marker.ToolTip = text;
      //    }

      //    var errorNode = new TreeViewItem();
      //    errorNode.Header = Path.GetFileNameWithoutExtension(file.FullName) + "(" + message.Location.StartLineColumn + "): " + text;
      //    errorNode.Tag = message;
      //    errorNode.MouseDoubleClick += errorNode_MouseDoubleClick;

      //    foreach (var nestedMessage in message.NestedMessages)
      //    {
      //      var nestadErrorNode = new TreeViewItem();
      //      nestadErrorNode.Header = Path.GetFileNameWithoutExtension(file.FullName) + "(" + nestedMessage.Location.StartLineColumn + "): " + nestedMessage.Text;
      //      nestadErrorNode.Tag = nestedMessage;
      //      nestadErrorNode.MouseDoubleClick += errorNode_MouseDoubleClick;
      //      errorNode.Items.Add(nestadErrorNode);
      //    }

      //    errorNodes.Add(errorNode);
      //  }

      //  _status.Text = cmpilerMessages.Count == 0 ? "OK" : cmpilerMessages.Count + " error[s]";
      //}
    }

    void errorNode_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      var node = (TreeViewItem)sender;
      if (!node.IsSelected)
        return;
      //var error = (CompilerMessage)node.Tag;
      //SelectText(error.Location);
      e.Handled = true;
      _text.Focus();
    }

    void ShowInfo()
    {
      _needUpdateReflection      = true;
      _needUpdateHtmlPrettyPrint = true;
      _needUpdateTextPrettyPrint = true;
      //_parseTree                 = null;

      UpdateInfo();
    }

    void UpdateInfo()
    {
      try
      {
        if (_needUpdateReflection           && ReferenceEquals(_tabControl.SelectedItem, _reflectionTabItem))
          UpdateReflection();
        else if (_needUpdateHtmlPrettyPrint && ReferenceEquals(_tabControl.SelectedItem, _htmlPrettyPrintTabItem))
          UpdateHtmlPrettyPrint();
        else if (_needUpdateTextPrettyPrint && ReferenceEquals(_tabControl.SelectedItem, _textPrettyPrintTabItem))
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

      //if (_parseResult == null)
      //  return;

      //var root = _parseResult.Reflect();
      //_reflectionTreeView.ItemsSource = new[] { root };
    }

    private void UpdateHtmlPrettyPrint()
    {
      //_needUpdateHtmlPrettyPrint = false;

      //if (_parseResult == null)
      //  return;

      //if (_parseTree == null)
      //  _parseTree = _parseResult.CreateParseTree();

      //var htmlWriter = new HtmlPrettyPrintWriter(PrettyPrintOptions.DebugIndent | PrettyPrintOptions.MissingNodes, "missing", "debug", "garbage");
      //_parseTree.PrettyPrint(htmlWriter, 0, null);

      //var spanStyles = new StringBuilder();
      //foreach (var style in _highlightingStyles)
      //{
      //  var brush = style.Value.Foreground as SimpleHighlightingBrush;
      //  if (brush == null)
      //    continue;
      //  var color = brush.Brush.Color;
      //  spanStyles.Append('.').Append(style.Key.Replace('.', '-')).Append("{color:rgb(").Append(color.R).Append(',').Append(color.G).Append(',').Append(color.B).AppendLine(");}");
      //}
      //var html = Properties.Resources.PrettyPrintDoughnut.Replace("{spanclasses}", spanStyles.ToString()).Replace("{prettyprint}", htmlWriter.ToString());
      //prettyPrintViewer.NavigateToString(html);
    }

    private void UpdateTextPrettyPrint()
    {
      //_needUpdateTextPrettyPrint = false;

      //if (_parseResult == null)
      //  return;

      //if (_parseTree == null)
      //  _parseTree = _parseResult.CreateParseTree();

      //_prettyPrintTextBox.Text = _parseTree.ToString(PrettyPrintOptions.DebugIndent | PrettyPrintOptions.MissingNodes);
    }

    private void textBox1_TextChanged(object sender, EventArgs e)
    {
      if (_initializing)
        return;

      //_parseResult = null; // prevent calculations on outdated ParseResult
      _textBox1Tooltip.IsOpen = false;
    }

    private void textBox1_LostFocus(object sender, RoutedEventArgs e)
    {
      _text.TextArea.Caret.Show();
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
      this.Close();
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

    void ClearAll()
    {
      ClearMarkers();
      _declarationsTreeView.Items.Clear();
      _matchedBracketsMarkers.Clear();
      _recoveryTreeView.Items.Clear();
      _errorsTreeView.Items.Clear();
      ClearHighlighting();
      //_reflectionTreeView.ItemsSource = null;
    }

    void ClearMarkers()
    {
      _textMarkerService.RemoveAll(marker => marker.Tag == (object)ErrorMarkerTag);
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

    void _tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      UpdateInfo();
      ShowNodeForCaret();
    }

    void _copyButton_Click(object sender, RoutedEventArgs e)
    {
      var sb = new StringBuilder();
      //var stats = ((StatisticsTask.Container[])_performanceTreeView.ItemsSource);

      //foreach (var stat in stats)
       // sb.AppendLine(stat.ToString());
      
      var result = sb.ToString();

      Clipboard.SetData(DataFormats.Text, result);
      Clipboard.SetData(DataFormats.UnicodeText, result);
    }

    void CopyReflectionNodeText(object sender, ExecutedRoutedEventArgs e)
    {
      //var value = _reflectionTreeView.SelectedItem as ReflectionStruct;
      //
      //if (value != null)
      //{
      //  var result = value.Description;
      //  Clipboard.SetData(DataFormats.Text, result);
      //  Clipboard.SetData(DataFormats.UnicodeText, result);
      //}
    }

    bool CheckTestFolder()
    {
      if (File.Exists(_settings.CurrentWorkspace ?? ""))
        return true;

      return false;
    }

    void OnAddTest(object sender, ExecutedRoutedEventArgs e)
    {
      if (CheckTestFolder())
        AddTest();
      else
        MessageBox.Show(this, "Can't add test.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
    }


    void CommandBinding_CanAddTest(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = _currentSuite != null;
      e.Handled = true;
    }

    void AddTest()
    {
      if (_currentSuite == null)
      {
        MessageBox.Show(this, "Select a test suite first.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

      if (_needUpdateTextPrettyPrint)
        UpdateTextPrettyPrint();

      var testSuitePath = _currentSuite.FullPath;
      var selectedProject = _currentProject == null ? null : _currentProject.Name;
      var dialog = new AddTest(_currentNode, _text.Text, _prettyPrintTextBox.Text) { Owner = this };

      if (dialog.ShowDialog() ?? false)
      {
        LoadTests();
        SelectTest(testSuitePath);
      }
    }

    void SelectTest(string fullPath)
    {
      if (_workspace == null)
        return;

      foreach (var suite in _workspace.TestSuites)
      {
        if (suite.FullPath == fullPath)
        {
          suite.IsSelected = true;
          break;
        }

        foreach (var solution in suite.Children)
        {
          if (solution.FullPath == fullPath)
          {
            solution.IsSelected = true;
            break;
          }

          foreach (var project in solution.Children)
          {
            if (project.FullPath == fullPath)
            {
              project.IsSelected = true;
              break;
            }
            foreach (var test in project.Children)
            {
              if (test.FullPath == fullPath)
              {
                test.IsSelected = true;
                break;
              }
            }
          }
        }
      }
    }

    void OnRunTests(object sender, ExecutedRoutedEventArgs e)
    {
      if (_workspace != null)
        RunTests();
      else
        MessageBox.Show(this, "Can't run tests. No test worcspace open.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    void RunTests()
    {
      if (_workspace == null)
        return;

      foreach (var suite in _workspace.TestSuites)
      {
        foreach (var test in suite.GetAllTests())
          RunTest(test);

        suite.TestStateChanged();
      }
    }

    void RunTest(TestVm test)
    {
      test.Run(); // GetRecoveryAlgorithm());
      ShowDiff(test);
    }

    void ShowDiff(TestVm test)
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
      if (_workspace == null)
        return;
      var currentTestSuite = _currentSuite;
      var dialog = new TestSuiteDialog(create, currentTestSuite, _settings) { Owner = this };
      if (dialog.ShowDialog() ?? false)
      {
        if (currentTestSuite != null)
          _workspace.TestSuites.Remove(currentTestSuite);
        var testSuite = new SuiteVm(_workspace, dialog.TestSuiteName, _settings.Config);
        testSuite.IsSelected = true;
        _workspace.Save();
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

      Delete();
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
      if (_initializing)
        return;

      ProcessSelectTestTreeNode(e);
      SaveSelectedTestAndTestSuite();
    }

      private void ProcessSelectTestTreeNode(RoutedPropertyChangedEventArgs<object> e)
      {
        _currentNode = e.NewValue as BaseVm;

        var suite = e.NewValue as SuiteVm;
        if (suite != null)
        {
          ChangeCurrentTest(suite, null, null, null);
          _para.Inlines.Clear();
          return;
        }

        var solution = e.NewValue as SolutionVm;
        if (solution != null)
        {
          ChangeCurrentTest(solution.Suite, solution, null, null);
          return;
        }

        var project = e.NewValue as ProjectVm;
        if (project != null)
        {
          ChangeCurrentTest(project.Suite, project.Solution, project, null);
          return;
        }

        var test = e.NewValue as TestVm;
        if (test != null)
        {
          ChangeCurrentTest(test.Suite, test.Project.Solution, test.Project, test);
          ShowDiff(test);
          return;
        }
      }

    private void ChangeCurrentTest(SuiteVm newTestSuite, SolutionVm newSolution, ProjectVm newProject, TestVm newTest)
    {
      Trace.Assert(newTestSuite != null);

      if (newTestSuite != _currentSuite)
        ResetHighlightingStyles();

      ClearAll();

      if (newSolution != null && newSolution.IsSingleFileTest && newProject == null)
        newProject = newSolution.Children[0];

      if (newProject != null && newProject.IsSingleFileTest && newTest == null)
        newTest = newProject.Children[0];

      var isTestAvalable = newTest != null;

      var code = isTestAvalable ? newTest.Code : "";
      _initializing = true;
      try
      {
        _text.Text = code;
      }
      finally
      {
        _initializing = false;
      }
      _text.IsReadOnly = !isTestAvalable;
      _text.Background = isTestAvalable ? SystemColors.WindowBrush : SystemColors.ControlBrush;

      var client = newTestSuite.Client;
      UpdateVm(_currentSuite,    newTestSuite);
      var timer = Stopwatch.StartNew();
      DeactivateVm(_currentTest,     newTest,     client);
      DeactivateVm(_currentProject,  newProject,  client);
      DeactivateVm(_currentSolution, newSolution, client);

      ActivateVm  (_currentSolution, newSolution, client);
      ActivateVm  (_currentProject,  newProject,  client);
      ActivateVm  (_currentTest,     newTest,     client);
      this.Title = timer.Elapsed.ToString();

      _currentSuite    = newTestSuite;
      _currentSolution = newSolution;
      _currentProject  = newProject;

      if (_currentTest != newTest)
      {
        _currentTest     = newTest;
        var responseMap = client.ResponseMap;
        responseMap.Clear();
        if (newTest != null)
        {
          responseMap[newTest.Id] = _responseDispatcher;
          responseMap[-1]         = _responseDispatcher;
        }
        TryReportError();
      }
    }

    void Response(ServerMessage msg)
    {
      ServerMessage.OutliningCreated            outlining;
      ServerMessage.KeywordsHighlightingCreated keywordHighlighting;
      ServerMessage.LanguageLoaded              languageInfo;
      ServerMessage.SymbolsHighlightingCreated  symbolsHighlighting;
      ServerMessage.ParsingMessages             parsingMessages = null;
      ServerMessage.SemanticAnalysisMessages    typingMessages  = null;

      if ((parsingMessages = msg as ServerMessage.ParsingMessages) != null)
      {
        TestVm file = _currentSolution.GetFile(msg.FileId);
        file.ParsingMessages = parsingMessages.messages;
      }
      else if ((typingMessages = msg as ServerMessage.SemanticAnalysisMessages) != null)
      {
        TestVm file = _currentSolution.GetFile(msg.FileId);
        file.SemanticAnalysisMessages = typingMessages.messages;
      }

      if (_currentTest == null || msg.FileId >= 0 && msg.FileId != _currentTest.Id || msg.Version >= 0 && msg.Version != _currentTest.Version)
        return;

      if ((outlining = msg as ServerMessage.OutliningCreated) != null)
      {
        _foldingStrategy.Outlining = outlining.outlining;
        _foldingStrategy.UpdateFoldings(_foldingManager, _text.Document);
      }
      else if ((keywordHighlighting = msg as ServerMessage.KeywordsHighlightingCreated) != null)
      {
        UpdateKeywordSpanInfos(keywordHighlighting);
      }
      else if ((symbolsHighlighting = msg as ServerMessage.SymbolsHighlightingCreated) != null)
      {
        UpdateSymbolsSpanInfos(symbolsHighlighting);
      }
      else if ((languageInfo = msg as ServerMessage.LanguageLoaded) != null)
      {
        UpdateHighlightingStyles(languageInfo);
      }
      else if (parsingMessages != null || typingMessages != null)
      {
        TryReportError();
      }
    }

    void Document_Changed(object sender, DocumentChangeEventArgs e)
    {
      if (_initializing)
        return;

      var version = _currentTest.Version;
      version++;

      Debug.Assert(e.OffsetChangeMap != null);
      _currentTest.OnTextChanged(version, e.InsertedText, e.InsertionLength, e.Offset, e.RemovalLength, _text.Text);
    }

    void UpdateVm(SuiteVm oldVm, SuiteVm newVm)
    {
      if (oldVm != newVm)
      {
        if (oldVm != null)
          oldVm.Deactivate();

        if (newVm != null)
          newVm.Activate();
      }
    }

    void DeactivateVm(IClientHost oldVm, IClientHost newVm, NitraClient client)
    {
      if (oldVm != newVm)
      {
        if (oldVm != null)
          oldVm.Deactivate();
      }
    }

    void ActivateVm(IClientHost oldVm, IClientHost newVm, NitraClient client)
    {
      if (oldVm != newVm)
      {
        if (newVm != null)
          newVm.Activate(client);
      }
    }

    void OnRemoveTestSuite(object sender, ExecutedRoutedEventArgs e)
    {
      if (_workspace == null || _currentSuite == null)
        return;

      if (MessageBox.Show(this, "Do you want to delete the '" + _currentSuite.Name + "' test suite?\r\nAll test will be deleted!", "Visualizer!", MessageBoxButton.YesNo,
        MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
        return;

      _currentSuite.Remove();
    }

    void CommandBinding_CanRemoveTestSuite(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = _currentSuite != null;
      e.Handled = true;
    }

    void OnRunTest(object sender, ExecutedRoutedEventArgs e)
    {
      RunTest();
    }

    void RunTest()
    {
      {
        var test = _testsTreeView.SelectedItem as TestVm;
        if (test != null)
        {
          RunTest(test);
          test.Suite.TestStateChanged();

          if (test.TestState == TestState.Failure)
            _testResultDiffTabItem.IsSelected = true;

          return;
        }
      }
      var testsContainer = _testsTreeView.SelectedItem as BaseVm;
      if (testsContainer != null)
      {
        foreach (var test in testsContainer.GetAllTests())
          RunTest(test);
        return;
      }

      if (_workspace != null)
        foreach (var test in _workspace.GetAllTests())
          RunTest(test);
    }

    void CommandBinding_CanRunTest(object sender, CanExecuteRoutedEventArgs e)
    {
      if (_testsTreeView == null)
        return;

      if (_testsTreeView.SelectedItem is TestVm)
      {
        e.CanExecute = true;
        e.Handled = true;
      }
      else if (_testsTreeView.SelectedItem is SuiteVm)
      {
        e.CanExecute = true;
        e.Handled = true;
      }
    }

    void _testsTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      RunTest();
      e.Handled = true;
    }

    void _configComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (_initializing)
        return;

      var config = (string)_configComboBox.SelectedItem;
      _settings.Config = config;
      LoadTests();
    }

    void OnUpdateTest(object sender, ExecutedRoutedEventArgs e)
    {
      var test = _testsTreeView.SelectedItem as TestVm;
      if (test != null)
      {
        try
        {
          test.Update(_text.Text, _prettyPrintTextBox.Text);
          test.Suite.TestStateChanged();
        }
        catch (Exception ex)
        {
          MessageBox.Show(this, "Fail to update the test '" + test.Name + "'." + Environment.NewLine + ex.GetType().Name + ":" + ex.Message, "Visualizer!",
            MessageBoxButton.YesNo,
            MessageBoxImage.Error);
        }
      }
    }

    void OnReparse(object sender, ExecutedRoutedEventArgs e)
    {
      Reparse();
    }

    void Reparse()
    {
      if (_currentSuite == null || _currentTest == null)
        return;

      _currentSuite.Client.Send(new ClientMessage.FileReparse(_currentTest.Id));
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
      if (_currentSuite == null)
        return;

      _currentSuite.ShowGrammar();
    }

    private void CommandBinding_CanShowGrammar(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = _currentSuite != null;
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
      //if(_parseResult == null || _astRoot == null)
      //  return;

      //var completionList = CompleteWord(pos, _astRoot);

      //_completionWindow = new CompletionWindow(_text.TextArea);
      //IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;

      //foreach (var completionData in completionList)
      //  if (!string.IsNullOrEmpty(completionData.Text) && char.IsLetter(completionData.Text[0]))
      //    data.Add(completionData);

      //_completionWindow.Show();
      //_completionWindow.Closed += delegate { _completionWindow = null; };
    }

    private List<CompletionData> CompleteWord(int pos)
    {
      return null;
      //NSpan replacementSpan;
      //var parseResult = astRoot.File.ParseResult;
      //var result = NitraUtils.CompleteWord(pos, parseResult, astRoot, out replacementSpan);
      //var completionList = new List<CompletionData>();

      //foreach (var elem in result)
      //{
      //  var symbol = elem as DeclarationSymbol;
      //  if (symbol != null && symbol.IsNameValid)
      //  {
      //    var content = symbol.ToXaml();
      //    var description = content;
      //    // TODO: починить отображение неоднозначностей
      //    //var amb = symbol as IAmbiguousSymbol;
      //    //if (amb != null)
      //    //  description = Utils.WrapToXaml(string.Join(@"<LineBreak/>", amb.Ambiguous.Select(a => a.ToXaml())));
      //    completionList.Add(new CompletionData(replacementSpan, symbol.Name, content, description, priority: 1.0));
      //  }

      //  var literal = elem as string;
      //  if (literal != null)
      //  {
      //    var escaped = Utils.Escape(literal);
      //    var xaml = "<Span Foreground='blue'>" + escaped + "</Span>";
      //    completionList.Add(new CompletionData(replacementSpan, literal, xaml, "keyword " + xaml, priority: 2.0));
      //  }
      //}

      //return completionList;
    }

    private void TryMatchBraces()
    {
      //var pos = _text.CaretOffset;
      //foreach (var bracket in _matchedBrackets)
      //{
      //  if (TryMatchBrace(bracket.OpenBracket, pos, bracket.CloseBracket.EndPos))
      //    break;
      //  if (TryMatchBrace(bracket.CloseBracket, pos, bracket.OpenBracket.StartPos))
      //    break;
      //}
    }

    private bool TryMatchBrace(NSpan brace, int pos, int gotoPos)
    {
      return false;
      //if (!brace.IntersectsWith(pos))
      //  return false;

      //_text.CaretOffset = gotoPos;
      //_text.ScrollTo(_text.TextArea.Caret.Line, _text.TextArea.Caret.Column);
      //return true;
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

          OpenWorkspace(solutionFilePath);
        }
      }
    }

    private void OpenWorkspace(string workspaceFilePath)
    {
      _settings.CurrentWorkspace = workspaceFilePath;
      _workspace = new WorkspaceVm(workspaceFilePath, null, _settings.Config);
      _testsTreeView.ItemsSource = _workspace.TestSuites;
      RecentFileList.InsertFile(workspaceFilePath);
    }

    private void OnWorkspaceOpen(object sender, ExecutedRoutedEventArgs e)
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
          OpenWorkspace(dialog.FileName);
        }
      }
    }

    ContextMenu _defaultContextMenu;
    BaseVm _currentNode;

    private void OnAddExistsTestSuite(object sender, ExecutedRoutedEventArgs e)
    {
      var unattachedTestSuites = _workspace.GetUnattachedTestSuites();
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
      var testSuite = new SuiteVm(_workspace, name, _settings.Config);
      testSuite.IsSelected = true;
      _workspace.Save();
    }

    private void CommandBinding_CanOnAddTestSuite(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = _workspace != null;
      e.Handled = true;
    }

    private void OnEditTestSuite(object sender, ExecutedRoutedEventArgs e)
    {
      EditTestSuite(false);
    }

    private void CommandBinding_CanOnEditTestSuite(object sender, CanExecuteRoutedEventArgs e)
    {
      e.Handled = true;
      e.CanExecute = _currentSuite != null;
    }

    private void RecentFileList_OnMenuClick(object sender, RecentFileList.MenuClickEventArgs e)
    {
      OpenWorkspace(e.Filepath);
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

    private static string MakeTestFileName(ProjectVm project)
    {
      var names = new bool['Z' - 'A'];
      foreach (var t in project.Children)
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
      var test = _testsTreeView.SelectedItem as TestVm;

      if (test != null)
      {
        var dirPath = Path.GetDirectoryName(test.FullPath);

        if (!Directory.Exists(dirPath))
          Directory.CreateDirectory(dirPath);

        var prj = test.Project;
        var firstFilePath = Path.Combine(dirPath, MakeTestFileName(prj) + ".test");

        if (File.Exists(test.FullPath))
          File.Move(test.FullPath, firstFilePath);
        else
          File.WriteAllText(firstFilePath, Environment.NewLine, Encoding.UTF8);

        if (File.Exists(test.Gold))
          File.Move(test.Gold, Path.ChangeExtension(firstFilePath, ".gold"));
        var stringManager = prj.Suite.Workspace.StringManager;
        prj.Children.Add(new TestVm(test.Suite, prj, firstFilePath, stringManager[firstFilePath]));
        AddNewFileToMultitest(prj).IsSelected = true;
        return;
      }

      var project = _testsTreeView.SelectedItem as ProjectVm;

      if (project != null)
        AddNewFileToMultitest(project).IsSelected = true;
    }

    private static TestVm AddNewFileToMultitest(ProjectVm project)
    {
      var name = MakeTestFileName(project);
      var path = Path.Combine(project.FullPath, name + ".test");
      File.WriteAllText(path, Environment.NewLine, Encoding.UTF8);
      var stringManager = project.Suite.Workspace.StringManager;
      var newTest = new TestVm(project.Suite, project, path, stringManager[path]);
      project.Children.Add(newTest);
      return newTest;
    }

    private void CopyReflectionText(object sender, RoutedEventArgs e)
    {
      //var reflectionStruct = _reflectionTreeView.SelectedItem as ReflectionStruct;
      //if (reflectionStruct != null)
      //  CopyTreeNodeToClipboard(reflectionStruct.Description);
    }

    private void DeleteFile_MenuItem_OnClick(object sender, RoutedEventArgs e)
    {
      Delete();
    }

    private void Delete()
    {
      var test = _testsTreeView.SelectedItem as TestVm;

      if (test != null)
      {
        if (File.Exists(test.FullPath))
           File.Delete(test.FullPath);

        var goldPath = Path.ChangeExtension(test.Gold, ".gold");

        if (File.Exists(goldPath))
          File.Delete(goldPath);

        var index = test.Project.Children.IndexOf(test);
        test.Project.Children.Remove(test);
        if (index < test.Project.Children.Count)
          test.Project.Children[index].IsSelected = true;
        else if (index > 0)
          test.Project.Children[index - 1].IsSelected = true;

        return;
      }

      var project = _testsTreeView.SelectedItem as ProjectVm;

      if (project != null)
      {
        if (Directory.Exists(project.FullPath))
          FileSystem.DeleteDirectory(project.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.DeletePermanently);

        var solution = project.Solution;
        var index = solution.Children.IndexOf(project);
        solution.Children.Remove(project);
        if (index < solution.Children.Count)
          solution.Children[index].IsSelected = true;
        else if (index > 0)
          solution.Children[index - 1].IsSelected = true;
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
