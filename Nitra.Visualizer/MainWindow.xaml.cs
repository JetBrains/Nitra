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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Nitra.Visualizer.ViewModels;
using Nitra.Visualizer.Views;
using ReactiveUI;
using Clipboard = System.Windows.Clipboard;
using ContextMenu = System.Windows.Controls.ContextMenu;
using Control = System.Windows.Controls.Control;
using DataFormats = System.Windows.DataFormats;
using File = System.IO.File;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using ToolTip = System.Windows.Controls.ToolTip;

namespace Nitra.Visualizer
{
  using ClientServer.Messages;
  using Interop;
  using System.Collections.Immutable;
  using System.Windows.Documents;
  using System.Windows.Interop;

  public partial class MainWindow : IViewFor<MainWindowViewModel>
  {
    const string ErrorMarkerTag = "Error";

    bool _initializing = true;
    bool _doTreeOperation;
    bool _doChangeCaretPos;
    //readonly Timer _nodeForCaretTimer;
    readonly TextMarkerService _textMarkerService;
    readonly NitraFoldingStrategy _foldingStrategy;
    readonly FoldingManager _foldingManager;
    readonly ToolTip _textBox1Tooltip;
    //ParseTree _parseTree;
    readonly PependentPropertyGrid _propertyGrid;
    //readonly MatchBracketsWalker _matchBracketsWalker = new MatchBracketsWalker();
    readonly List<ITextMarker> _matchedBracketsMarkers = new List<ITextMarker>();
    readonly Action<AsyncServerMessage> _responseDispatcher;
    readonly Timer _fillAstTimer;
    //List<MatchBracketsWalker.MatchBrackets> _matchedBrackets;

    public MainWindow()
    {
      ViewModel = new MainWindowViewModel();
      var events = this.Events();

      Splat.Locator.CurrentMutable.Register(() => new PopupItemView(), typeof(IViewFor<PopupItemViewModel>));

      ToolTipService.ShowDurationProperty.OverrideMetadata(
        typeof(DependencyObject),
        new FrameworkPropertyMetadata(Int32.MaxValue));

      _fillAstTimer = new Timer { AutoReset = false, Enabled = false, Interval = 300 };
      _fillAstTimer.Elapsed += _fillAstTimer_Elapsed;

      InitializeComponent();

      var editorViewModel = ViewModel.Editor;
      _textEditor.ViewModel = editorViewModel;

      _responseDispatcher = msg => _textEditor.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<AsyncServerMessage>(Response), msg);

      _mainRow.Height  = new GridLength(ViewModel.Settings.TabControlHeight);
      
      _configComboBox.ItemsSource = new[] {"Debug", "Release"};
      var config = ViewModel.Settings.Config;
      _configComboBox.SelectedItem = config == "Release" ? "Release" : "Debug";

      _tabControl.SelectedIndex = ViewModel.Settings.ActiveTabIndex;
      _foldingStrategy          = new NitraFoldingStrategy();
      _textBox1Tooltip          = new ToolTip { PlacementTarget = _textEditor };
      //_nodeForCaretTimer        = new Timer {AutoReset = false, Enabled = false, Interval = 500};
      //_nodeForCaretTimer.Elapsed += _nodeForCaretTimer_Elapsed;

      editorViewModel.WhenAnyValue(vm => vm.CaretOffset)
                     .Throttle(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
                     .Subscribe(_ => ShowNodeForCaret());

      this.OneWayBind(ViewModel, vm => vm.Editor.CaretOffset, v => v._pos.Text, pos => pos.ToString(CultureInfo.InvariantCulture));
      this.OneWayBind(ViewModel, vm => vm.StatusText, v => v._status.Text);

      events.KeyDown
            .Where(a => a.Key == Key.F12 && Keyboard.Modifiers == ModifierKeys.None)
            .InvokeCommand(ViewModel.FindSymbolDefinitions);

      events.KeyDown
            .Where(a => a.Key == Key.F12 && Keyboard.Modifiers == ModifierKeys.Shift)
            .InvokeCommand(ViewModel.FindSymbolReferences);

      _foldingManager = FoldingManager.Install(_textEditor.TextArea);
      _textMarkerService = new TextMarkerService(_textEditor.Document);

      _textEditor.TextArea.TextView.BackgroundRenderers.Add(_textMarkerService);
      _textEditor.TextArea.TextView.LineTransformers.Add(_textMarkerService);
      _textEditor.Options.ConvertTabsToSpaces = true;
      _textEditor.Options.EnableRectangularSelection = true;
      _textEditor.Options.IndentationSize = 2;
      _testsTreeView.SelectedValuePath = "FullPath";
      _propertyGrid = new PependentPropertyGrid();
      _windowsFormsHost.Child = _propertyGrid;

      if (string.IsNullOrWhiteSpace(ViewModel.Settings.CurrentWorkspace))
        ViewModel.Workspace = null;
      else
        LoadTests();

      _textEditor.Document.Changed += Document_Changed;
      _textEditor.Document.UpdateStarted += DocumentOnUpdateStarted;
      _textEditor.Document.UpdateFinished += DocumentOnUpdateFinished;
    }

    private void DocumentOnUpdateFinished(object sender, EventArgs eventArgs)
    {
      if (ViewModel.CurrentFile == null || _initializing)
        return;

      ViewModel.CurrentFile.StartBatchCodeUpdate();
    }

    private void DocumentOnUpdateStarted(object sender, EventArgs eventArgs)
    {
      if (ViewModel.CurrentFile == null || _initializing)
        return;

      ViewModel.CurrentFile.FinishBatchCodeUpdate();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      _initializing = false;

      if ((Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) != 0)
        return;
      if (ViewModel.Workspace != null)
        SelectTest(ViewModel.Settings.SelectedTestNode);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
      base.OnSourceInitialized(e);
      this.SetPlacement(ViewModel.Settings.MainWindowPlacement);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      _initializing = true;

      ViewModel.Settings.MainWindowPlacement = this.GetPlacement();
      ViewModel.Settings.Config = (string)_configComboBox.SelectedValue;
      ViewModel.Settings.TabControlHeight = _mainRow.Height.Value;
      ViewModel.Settings.LastTextInput = _textEditor.Text;
      ViewModel.Settings.ActiveTabIndex = _tabControl.SelectedIndex;
      ViewModel.Settings.Save();
      
      ViewModel.CurrentSuite    = null;
      ViewModel.CurrentSolution = null;
      ViewModel.CurrentProject  = null;
      ViewModel.CurrentFile     = null;

      if (ViewModel.Workspace != null)
      {
        foreach (var testSuite in ViewModel.Workspace.TestSuites)
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
      if (ViewModel.CurrentFile != null)
        ViewModel.Settings.SelectedTestNode = ViewModel.CurrentFile.FullPath;
      else if (ViewModel.CurrentProject != null)
        ViewModel.Settings.SelectedTestNode = ViewModel.CurrentProject.FullPath;
      else if (ViewModel.CurrentSolution != null)
        ViewModel.Settings.SelectedTestNode = ViewModel.CurrentSolution.FullPath;
      else if (ViewModel.CurrentSuite != null)
        ViewModel.Settings.SelectedTestNode = ViewModel.CurrentSuite.FullPath;

      ViewModel.Settings.Save();

      if (ViewModel.Workspace != null && ViewModel.Workspace.IsDirty)
        ViewModel.Workspace.Save();
    }

    private void LoadTests()
    {
      var selected = _testsTreeView.SelectedItem as BaseVm;
      var selectedPath = selected == null ? null : selected.FullPath;

      if (!File.Exists(ViewModel.Settings.CurrentWorkspace ?? ""))
      {
        MessageBox.Show(this, "Workspace '" + ViewModel.Settings.CurrentWorkspace + "' not exists!");
        return;
      }

      ViewModel.Workspace = new WorkspaceVm(ViewModel.Settings.CurrentWorkspace, selectedPath, ViewModel.Settings.Config);
      this.Title = ViewModel.Workspace.Name + " - " + Constants.AppName;
      _testsTreeView.ItemsSource = ViewModel.Workspace.TestSuites;
    }

    private void textBox1_GotFocus(object sender, RoutedEventArgs e)
    {
      ShowNodeForCaret();
    }
    
    private void ShowNodeForCaret()
    {
      if (_doTreeOperation)
        return;

      _doChangeCaretPos = true;
      try
      {
        if (IsAstReflectionTabItemActive())
          ShowAstNodeForCaret();
        else if (IsReflectionTabItemActive())
          ShowParseTreeNodeForCaret();
      }
      finally
      {
        _doChangeCaretPos = false;
      }
    }

    private void ShowAstNodeForCaret(bool enforce = false)
    {
      if (!enforce && _astTreeView.IsKeyboardFocusWithin)
        return;

      if (_astTreeView.Items.Count < 1)
        return;

      Debug.Assert(_astTreeView.Items.Count == 1);

      var root = (AstNodeViewModel)_astTreeView.Items[0];
      var file = ViewModel.CurrentFile;
      var context = root.Context;
      if (file == null || context.FileId != file.Id || context.FileVersion != file.Version)
        return;

      var ast = FindAstNode(root, _textEditor.CaretOffset);

      if (ast != null)
        ast.IsSelected = true;
    }

    private AstNodeViewModel FindAstNode(AstNodeViewModel ast, int pos, List<NSpan> checkedSpans = null)
    {
      var span = ast.Span;

      if (!span.IntersectsWith(pos))
        return null;

      if (span == default(NSpan))
        return null;

      checkedSpans = checkedSpans ?? new List<NSpan>();

      // check for circular dependency
      for (var i = 0; i < checkedSpans.Count; i++)
      {
        // if current span was previously checked
        if (span == checkedSpans[i])
        {
          // and it's not a topmost span
          for (var k = i; k < checkedSpans.Count; k++)
            if (span != checkedSpans[k])
              // Stop FindNode recursion
              return null;
          break;
        }
      }

      if (span != default(NSpan))
        checkedSpans.Add(span);

      ast.LoadItems();

      var items = ast.Items;

      if (items != null)
        foreach (AstNodeViewModel subItem in items)
        {
          var result = FindAstNode(subItem, pos, checkedSpans);
          if (result != null)
          {
            ast.IsExpanded = true;
            return result;
          }
        }

      return ast;
    }

    private void ShowParseTreeNodeForCaret(bool enforce = false)
    {
      if (_reflectionTreeView.ItemsSource == null)
        return;

      if (!enforce && _reflectionTreeView.IsKeyboardFocusWithin)
        return;


      var node = FindParseTreeNode((ParseTreeReflectionStruct[])_reflectionTreeView.ItemsSource, _textEditor.CaretOffset);
      
      if (node != null)
      {
        var selected = _reflectionTreeView.SelectedItem as ParseTreeReflectionStruct;
      
        if (node == selected)
          return;
      
        _reflectionTreeView.SelectedItem = node;
        _reflectionTreeView.BringIntoView(node);
      }
    }

    private ParseTreeReflectionStruct FindParseTreeNode(IEnumerable<ParseTreeReflectionStruct> items, int p)
    {
      foreach (ParseTreeReflectionStruct node in items)
      {
        if (node.Span.StartPos <= p && p < node.Span.EndPos) // IntersectsWith(p) includes EndPos
        {
          if (node.Children.Length == 0)
            return node;
      
          _reflectionTreeView.Expand(node);
      
          return FindParseTreeNode(node.Children, p);
        }
      }

      return null;
    }

    private void TryReportError()
    {
      if (ViewModel.CurrentFile == null)
        return;

      var cmpilerMessages = new List<CompilerMessage>();
      cmpilerMessages.AddRange(ViewModel.CurrentFile.ParsingMessages);
      cmpilerMessages.AddRange(ViewModel.CurrentFile.SemanticAnalysisMessages);
      cmpilerMessages.Sort();

      ClearMarkers();

      var errorNodes      = _errorsTreeView.Items;
      var currentFileId   = ViewModel.CurrentFile.Id;
      var fullName        = ViewModel.CurrentFile.FullPath;
      var doc             = _textEditor.Document;

      errorNodes.Clear();

      foreach (var message in cmpilerMessages)
      {
        var text     = message.Text;
        var location = message.Location;
        var file     = location.File;
        var span     = location.Span;
        if (currentFileId == file.FileId) 
        {
          if (span.StartPos >= doc.TextLength)
            continue;

          var spanLength = (span.StartPos + span.Length) <= doc.TextLength 
                           ? span.Length
                           : doc.TextLength - span.StartPos;
          var marker = _textMarkerService.Create(span.StartPos, spanLength);
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

        if (message.NestedMessages != null)
        {
          foreach (var nestedMessage in message.NestedMessages)
          {
            var nestedPos = doc.GetLocation(span.StartPos);
            var nestadErrorNode = new TreeViewItem();
            nestadErrorNode.Header = Path.GetFileNameWithoutExtension(fullName) + "(" + nestedPos.Line + "," + nestedPos.Column + "): " + nestedMessage.Text;
            nestadErrorNode.Tag = nestedMessage;
            nestadErrorNode.MouseDoubleClick += errorNode_MouseDoubleClick;
            errorNode.Items.Add(nestadErrorNode);
          }
        }

        errorNodes.Add(errorNode);
      }

      _status.Text = cmpilerMessages.Count == 0 ? "OK" : cmpilerMessages.Count + " error[s]";
    }

    void errorNode_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      var node = (TreeViewItem)sender;
      if (!node.IsSelected)
        return;
      var error = (CompilerMessage)node.Tag;

      ViewModel.Editor.SelectText(error.Location);

      e.Handled = true;
      _textEditor.Focus();
    }

    private bool IsAstReflectionTabItemActive()
    {
      return ReferenceEquals(_tabControl.SelectedItem, _astReflectionTabItem);
    }

    private bool IsHtmlPrettyPrintTabActive()
    {
      return ReferenceEquals(_tabControl.SelectedItem, _htmlPrettyPrintTabItem);
    }

    private bool IsReflectionTabItemActive()
    {
      return ReferenceEquals(_tabControl.SelectedItem, _reflectionTabItem);
    }

    private bool IsPrettyPrintTabActive()
    {
      return ReferenceEquals(_tabControl.SelectedItem, _textPrettyPrintTabItem);
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
      _textEditor.TextArea.Caret.Show();
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
      _astTreeView.ItemsSource = new AstNodeViewModel[0];
      _matchedBracketsMarkers.Clear();
      _recoveryTreeView.Items.Clear();
      _errorsTreeView.Items.Clear();
      ClearHighlighting();
      _reflectionTreeView.ItemsSource = null;
    }

    void ClearMarkers()
    {
      _textMarkerService.RemoveAll(marker => marker.Tag == (object)ErrorMarkerTag);
    }

    private void textBox1_MouseHover(object sender, MouseEventArgs e)
    {
      var pos = _textEditor.TextArea.TextView.GetPositionFloor(e.GetPosition(_textEditor.TextArea.TextView) + _textEditor.TextArea.TextView.ScrollOffset);
      if (pos.HasValue)
      {
        var offset = _textEditor.Document.GetOffset(new TextLocation(pos.Value.Line, pos.Value.Column));
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
      if (ViewModel.CurrentSuite == null)
        return;
      ClearAstHighlighting();
      Reparse();
      var client = ViewModel.CurrentSuite.Client;
      UpdateTrees(client);
    }

    void UpdateTrees(NitraClient client)
    {
      UpdatePrettyPrintStatus(client);
      UpdateParseTreeReflection(client);
    }

    void UpdatePrettyPrintStatus(NitraClient client)
    {
      if (IsPrettyPrintTabActive())
        client.Send(new ClientMessage.PrettyPrint(PrettyPrintState.Text));
      else if (IsHtmlPrettyPrintTabActive())
        client.Send(new ClientMessage.PrettyPrint(PrettyPrintState.Html));
      else
        client.Send(new ClientMessage.PrettyPrint(PrettyPrintState.Disabled));
    }

    void UpdateParseTreeReflection(NitraClient client)
    {
      client.Send(new ClientMessage.ParseTreeReflection(IsReflectionTabItemActive()));
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
      var value = _reflectionTreeView.SelectedItem as ParseTreeReflectionStruct;
      
      if (value != null)
      {
        var result = value.Description;
        Clipboard.SetData(DataFormats.Text, result);
        Clipboard.SetData(DataFormats.UnicodeText, result);
      }
    }

    bool CheckTestFolder()
    {
      if (File.Exists(ViewModel.Settings.CurrentWorkspace ?? ""))
        return true;

      return false;
    }

    void OnAddSolution(object sender, ExecutedRoutedEventArgs e)
    {
      if (CheckTestFolder())
        AddTest();
      else
        MessageBox.Show(this, "Can't add test.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
    }


    void CommandBinding_CanAddSolution(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = ViewModel.CurrentSuite != null;
      e.Handled = true;
    }

    void AddTest()
    {
      if (ViewModel.CurrentSuite == null)
      {
        MessageBox.Show(this, "Select a test suite first.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

      var testSuitePath = ViewModel.CurrentSuite.FullPath;
      var selectedProject = ViewModel.CurrentProject == null ? null : ViewModel.CurrentProject.Name;
      var dialog = new AddTest(_currentNode, _textEditor.Text, _prettyPrintTextBox.Text) { Owner = this };

      if (dialog.ShowDialog() ?? false)
      {
        LoadTests();
        SelectTest(testSuitePath);
      }
    }

    void SelectTest(string fullPath)
    {
      if (ViewModel.Workspace == null)
        return;

      foreach (var suite in ViewModel.Workspace.TestSuites)
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
      if (ViewModel.Workspace != null)
        RunTests();
      else
        MessageBox.Show(this, "Can't run tests. No test worcspace open.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    void RunTests()
    {
      if (ViewModel.Workspace == null)
        return;

      foreach (var suite in ViewModel.Workspace.TestSuites)
      {
        foreach (var test in suite.GetAllTests())
          RunTest(test);

        suite.TestStateChanged();
      }
    }

    void RunTest(FileVm test)
    {
      test.Run(); // GetRecoveryAlgorithm());
      ShowDiff(test);
    }

    void ShowDiff(FileVm test)
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
      var workspace = ViewModel.Workspace;

      if (workspace == null)
        return;
      
      var suite = create 
                  ? new SuiteVm(workspace, "New suite", ViewModel.Settings.Config)
                  : ViewModel.CurrentSuite;
      
      var viewmodel = new TestSuiteCreateOrEditViewModel(suite.Client) {
        Title = create ? "New test suite" : "Edit test suite",
        RootFolder = suite.Workspace.RootFolder,
        SuiteName = suite.Name
      };

      viewmodel.ParserLibs
               .AddRange(suite.Config.Languages.Select(li => new ParserLibViewModel(li.Path)));

      viewmodel.References
               .AddRange(suite.Config.References);
      
      var dialog = new TestSuiteDialog(suite, viewmodel) {
        Owner = this
      };

      if (dialog.ShowDialog() ?? false) {
        suite.Config.Languages = viewmodel.Languages.ToArray();
        suite.Config.References = viewmodel.References.ToArray();
        suite.Config.ProjectSupport = viewmodel.ProjectSupports
                                               .Where(vm => vm.IsSelected)
                                               .Select(vm => vm.Source)
                                               .FirstOrDefault();
        suite.Save();

        suite.IsSelected = true;
        workspace.Save();
      } else if (create) {
        workspace.TestSuites.Remove(suite);
      }
    }


    private void OnRemoveSolution(object sender, ExecutedRoutedEventArgs e)
    {
      var test = _testsTreeView.SelectedItem as FileVm;
      if (test == null)
        return;
      if (MessageBox.Show(this, "Do you want to delete the '" + test.Name + "' test?", "Visualizer!", MessageBoxButton.YesNo,
        MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
        return;

      Delete();
    }

    private void CommandBinding_CanRemoveSolution(object sender, CanExecuteRoutedEventArgs e)
    {
      if (_testsTreeView == null)
        return;
      e.CanExecute = _testsTreeView.SelectedItem is FileVm;
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

      var test = e.NewValue as FileVm;
      if (test != null)
      {
        ChangeCurrentTest(test.Suite, test.Project.Solution, test.Project, test);
        ShowDiff(test);
        return;
      }
    }

    private void ChangeCurrentTest(SuiteVm newTestSuite, SolutionVm newSolution, ProjectVm newProject, FileVm newTest)
    {
      Trace.Assert(newTestSuite != null);

      if (newTestSuite != ViewModel.CurrentSuite)
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
        _textEditor.Text = code;
      }
      finally
      {
        _initializing = false;
      }
      _textEditor.IsReadOnly = !isTestAvalable;
      _textEditor.Background = isTestAvalable ? SystemColors.WindowBrush : SystemColors.ControlBrush;

      var client = newTestSuite.Client;
      var responseMap = client.ResponseMap;
      responseMap.Clear();
      responseMap[-1] = _responseDispatcher;

      if (ViewModel.CurrentSuite == null)
        UpdateTrees(client); // first time


      UpdateVm(ViewModel.CurrentSuite,    newTestSuite);
      var timer = Stopwatch.StartNew();
      DeactivateVm(ViewModel.CurrentFile,     newTest,     client);
      DeactivateVm(ViewModel.CurrentProject,  newProject,  client);
      DeactivateVm(ViewModel.CurrentSolution, newSolution, client);

      ActivateVm  (ViewModel.CurrentSolution, newSolution, client);
      ActivateVm  (ViewModel.CurrentProject,  newProject,  client);
      ActivateVm  (ViewModel.CurrentFile,     newTest,     client);
      this.Title = timer.Elapsed.ToString();

      ViewModel.CurrentSuite    = newTestSuite;
      ViewModel.CurrentSolution = newSolution;
      ViewModel.CurrentProject  = newProject;

      if (ViewModel.CurrentFile != newTest)
      {
        ViewModel.CurrentFile     = newTest;
        if (newTest != null)
        {
          responseMap[newTest.Id] = _responseDispatcher;
          FillTrees(client);
        }
        TryReportError();
      }
    }

    private void FillTrees(NitraClient client)
    {
      if (IsAstReflectionTabItemActive())
        FillAst();
      else
        UpdateTrees(client);
    }

    void Response(AsyncServerMessage msg)
    {
      AsyncServerMessage.OutliningCreated outlining;
      AsyncServerMessage.KeywordsHighlightingCreated keywordHighlighting;
      AsyncServerMessage.LanguageLoaded languageInfo;
      AsyncServerMessage.SymbolsHighlightingCreated symbolsHighlighting;
      AsyncServerMessage.ParsingMessages parsingMessages = null;
      AsyncServerMessage.SemanticAnalysisMessages typingMessages = null;
      AsyncServerMessage.PrettyPrintCreated prettyPrintCreated;
      AsyncServerMessage.ReflectionStructCreated reflectionStructCreated;
      AsyncServerMessage.RefreshReferencesFailed refreshReferencesFailed;
      AsyncServerMessage.RefreshProjectFailed refreshProjectFailed;
      AsyncServerMessage.Exception exception;

      var solution = ViewModel.CurrentSolution;
      if (solution == null || msg.SolutionId >= 0 && msg.SolutionId != solution.Id)
        return; // no solution or message for the old solution

      if ((parsingMessages = msg as AsyncServerMessage.ParsingMessages) != null)
      {
        FileVm file = ViewModel.CurrentSolution.GetFile(msg.FileId);
        file.ParsingMessages = parsingMessages.messages;
      }
      else if ((typingMessages = msg as AsyncServerMessage.SemanticAnalysisMessages) != null)
      {
        FileVm file = ViewModel.CurrentSolution.GetFile(msg.FileId);
        file.SemanticAnalysisMessages = typingMessages.messages;
      }
      else if ((languageInfo = msg as AsyncServerMessage.LanguageLoaded) != null)
        UpdateHighlightingStyles(languageInfo);
      else if (msg is AsyncServerMessage.SemanticAnalysisDone)
      {
        if (IsAstReflectionTabItemActive())
        {
          _fillAstTimer.Stop();
          _fillAstTimer.Start();
        }
      }
      else if ((refreshReferencesFailed = msg as AsyncServerMessage.RefreshReferencesFailed) != null)
      {
        ViewModel.CurrentProject = null;
        ViewModel.CurrentFile = null;
        MessageBox.Show(this, "Project loading is failed in call RefreshReferences().\r\nException: "
          + refreshReferencesFailed.exception);
      }
      else if ((refreshProjectFailed = msg as AsyncServerMessage.RefreshProjectFailed) != null)
        MessageBox.Show(this, "Project loading is failed in call RefreshProject().\r\nException: " + refreshProjectFailed.exception);
      else if ((exception = msg as AsyncServerMessage.Exception) != null)
        MessageBox.Show(this, "Exception occurred on the server: " + exception.exception);

      if (ViewModel.CurrentFile == null || msg.FileId >= 0 && msg.FileId != ViewModel.CurrentFile.Id || msg.Version >= 0 && msg.Version != ViewModel.CurrentFile.Version)
        return;

      if ((outlining = msg as AsyncServerMessage.OutliningCreated) != null)
      {
        _foldingStrategy.Outlining = outlining.outlining;
        _foldingStrategy.UpdateFoldings(_foldingManager, _textEditor.Document);
      }
      else if ((keywordHighlighting = msg as AsyncServerMessage.KeywordsHighlightingCreated) != null)
      {
        UpdateKeywordSpanInfos(keywordHighlighting);
      }
      else if ((symbolsHighlighting = msg as AsyncServerMessage.SymbolsHighlightingCreated) != null)
      {
        UpdateSymbolsSpanInfos(symbolsHighlighting);
      }
      else if ((prettyPrintCreated = msg as AsyncServerMessage.PrettyPrintCreated) != null)
      {
        switch (prettyPrintCreated.type)
        {
          case PrettyPrintState.Text:
            _prettyPrintTextBox.Text = prettyPrintCreated.text;
            break;
          case PrettyPrintState.Html:
            prettyPrintViewer.NavigateToString(prettyPrintCreated.text);
            break;
        }
      }
      else if ((reflectionStructCreated = msg as AsyncServerMessage.ReflectionStructCreated) != null)
      {
        _reflectionTreeView.ItemsSource = new[] { reflectionStructCreated.root };
        ShowParseTreeNodeForCaret(enforce: true);
      }
      else if (parsingMessages != null || typingMessages != null)
      {
        TryReportError();
      }
    }

    void _fillAstTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
      Dispatcher.Invoke(new Action(FillAst));
    }

    private void FillAst()
    {
      var file = ViewModel.CurrentFile;
      if (file == null)
        return;

      Debug.Assert(ViewModel.CurrentSolution != null);
      Debug.Assert(ViewModel.CurrentProject  != null);

      const int Root = 0;
      var version = file.Version;
      var client  = ViewModel.CurrentSuite.Client;
      var span    = new NSpan(0, _textEditor.Document.TextLength);
      var root    = new ObjectDescriptor.Ast(span, Root, "<File>", "<File>", "<File>", null);
      var context = new AstNodeViewModel.AstContext(client, ViewModel.CurrentSolution.Id, ViewModel.CurrentProject.Id, file.Id, version);
      var rootVm  = new ItemAstNodeViewModel(context, root, -1);
      rootVm.IsExpanded = true;
      _astTreeView.ItemsSource = new[] { rootVm };
      ShowAstNodeForCaret(enforce: true);
    }

    void Document_Changed(object sender, DocumentChangeEventArgs e)
    {
      if (_initializing)
        return;

      var version = new FileVersion(ViewModel.CurrentFile.Version.Value + 1);

      Debug.Assert(e.OffsetChangeMap != null);
      ViewModel.CurrentFile.OnTextChanged(version, e.InsertedText, e.InsertionLength, e.Offset, e.RemovalLength, _textEditor.Text);
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
      if (ViewModel.Workspace == null || ViewModel.CurrentSuite == null)
        return;

      if (MessageBox.Show(this, "Do you want to delete the '" + ViewModel.CurrentSuite.Name + "' test suite?\r\nAll test will be deleted!", "Visualizer!", MessageBoxButton.YesNo,
        MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
        return;

      ViewModel.CurrentSuite.Remove();
    }

    void CommandBinding_CanRemoveTestSuite(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = ViewModel.CurrentSuite != null;
      e.Handled = true;
    }

    void OnRunTest(object sender, ExecutedRoutedEventArgs e)
    {
      RunTest();
    }

    void RunTest()
    {
      {
        var test = _testsTreeView.SelectedItem as FileVm;
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

      if (ViewModel.Workspace != null)
        foreach (var test in ViewModel.Workspace.GetAllTests())
          RunTest(test);
    }

    void CommandBinding_CanRunTest(object sender, CanExecuteRoutedEventArgs e)
    {
      if (_testsTreeView == null)
        return;

      if (_testsTreeView.SelectedItem is FileVm)
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
      ViewModel.Settings.Config = config;
      LoadTests();
    }

    void OnUpdateSolution(object sender, ExecutedRoutedEventArgs e)
    {
      var test = _testsTreeView.SelectedItem as FileVm;
      if (test != null)
      {
        try
        {
          test.Update(_textEditor.Text, _prettyPrintTextBox.Text);
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
      if (ViewModel.CurrentSuite == null || ViewModel.CurrentFile == null)
        return;

      ViewModel.CurrentSuite.Client.Send(new ClientMessage.FileReparse(ViewModel.CurrentFile.Id));
    }


    private void _reflectionTreeView_SelectedItemChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
      if (_doChangeCaretPos)
        return;

      var node = e.NewValue as ParseTreeReflectionStruct;

      if (node == null)
        return;

      if (_textEditor.IsKeyboardFocusWithin)
        return;

      _doTreeOperation = true;
      try
      {
        _textEditor.TextArea.Caret.Offset = node.Span.StartPos;
        _textEditor.ScrollTo(_textEditor.TextArea.Caret.Line, _textEditor.TextArea.Caret.Column);
        _textEditor.TextArea.AllowCaretOutsideSelection();
        _textEditor.Select(node.Span.StartPos, node.Span.Length);
      }
      finally
      {
        _doTreeOperation = false;
      }
    }

    private void OnShowGrammar(object sender, ExecutedRoutedEventArgs e)
    {
      if (ViewModel.CurrentSuite == null)
        return;

      ViewModel.CurrentSuite.ShowGrammar();
    }

    private void CommandBinding_CanShowGrammar(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = ViewModel.CurrentSuite != null;
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
          ShowCompletionWindow(_textEditor.CaretOffset);
          e.Handled = true;
        }
        else if (e.Key == Key.Oem6) // Oem6 - '}'
          TryMatchBraces();
      }
    }

    private void ShowCompletionWindow(int pos)
    {
      if (ViewModel.CurrentSuite == null || ViewModel.CurrentFile == null)
        return;

      var client = ViewModel.CurrentSuite.Client;

      client.Send(new ClientMessage.CompleteWord(ViewModel.CurrentFile.Id, ViewModel.CurrentFile.Version, pos));
      var result = client.Receive<ServerMessage.CompleteWord>();
      var replacementSpan = result.replacementSpan;

      _completionWindow = new CompletionWindow(_textEditor.TextArea);
      IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;

      CompletionElem.Literal lit;
      CompletionElem.Symbol  s;

      Func<CompletionElem, string> completionKeySelector = el => {
        if ((lit = el as CompletionElem.Literal) != null) return lit.text;
        if ((s = el as CompletionElem.Symbol) != null) return s.name;
        return "";
      };

      var completionList = result.completionList
                                 .Where(c => {
                                   var key = completionKeySelector(c);
                                   return !key.StartsWith("?") &&
                                          !key.StartsWith("<");
                                 })
                                 .Distinct(completionKeySelector);

      foreach (var completionData in completionList)
      {
        if ((lit = completionData as CompletionElem.Literal) != null)
        {
          var escaped = Utils.Escape(lit.text);
          var xaml = "<Span Foreground='blue'>" + escaped + "</Span>";
          data.Add(new CompletionData(replacementSpan, lit.text, xaml, "keyword " + xaml, priority: 1.0));
        }
        else if ((s = completionData as CompletionElem.Symbol) != null)
          data.Add(new CompletionData(replacementSpan, s.name, s.content, s.description, priority: 1.0));
      }

      _completionWindow.Show();
      _completionWindow.Closed += delegate { _completionWindow = null; };
    }

    private void TryMatchBraces()
    {
      //var pos = _textEditor.CaretOffset;
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

      //_textEditor.CaretOffset = gotoPos;
      //_textEditor.ScrollTo(_textEditor.TextArea.Caret.Line, _textEditor.TextArea.Caret.Column);
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
      var vm = node as BaseVm;
      if (vm != null)
      {
        var text = vm.FullPath.ToString();
        Clipboard.SetData(DataFormats.Text, text);
        Clipboard.SetData(DataFormats.UnicodeText, text);
      }
      else if (node != null)
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
      ViewModel.Settings.CurrentWorkspace = workspaceFilePath;
      ViewModel.Workspace = new WorkspaceVm(workspaceFilePath, null, ViewModel.Settings.Config);
      _testsTreeView.ItemsSource = ViewModel.Workspace.TestSuites;
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
      var unattachedTestSuites = ViewModel.Workspace.GetUnattachedTestSuites();
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
      var testSuite = new SuiteVm(ViewModel.Workspace, name, ViewModel.Settings.Config);
      testSuite.IsSelected = true;
      ViewModel.Workspace.Save();
    }

    private void CommandBinding_CanOnAddTestSuite(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = ViewModel.Workspace != null;
      e.Handled = true;
    }

    private void OnEditTestSuite(object sender, ExecutedRoutedEventArgs e)
    {
      EditTestSuite(false);
    }

    private void CommandBinding_CanOnEditTestSuite(object sender, CanExecuteRoutedEventArgs e)
    {
      e.Handled = true;
      e.CanExecute = ViewModel.CurrentSuite != null;
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
      // HACK: Select TreeView Node on right click before displaying ContextMenu
      // (c) http://stackoverflow.com/questions/592373/select-treeview-node-on-right-click-before-displaying-contextmenu
      TreeViewItem treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);

      if (treeViewItem != null)
      {
        treeViewItem.Focus();
        e.Handled = true;
      }
    }

    static TreeViewItem VisualUpwardSearch(DependencyObject source)
    {
      while (source != null && !(source is TreeViewItem))
        source = VisualTreeHelper.GetParent(source);

      return source as TreeViewItem;
    }

    private static string MakeTestFileName(ProjectVm project)
    {
      var names = new bool['Z' - 'A'];
      foreach (var t in project.Children)
      {
        var name = Path.GetFileNameWithoutExtension(t.Name);
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

    private static FileVm AddNewFileToMultitest(string ext, ProjectVm project, string defaultContent)
    {
      NitraClient client = project.Solution.Suite.Client;

      var name = MakeTestFileName(project);

      if (!Directory.Exists(project.FullPath))
        Directory.CreateDirectory(project.FullPath);

      var path = Path.Combine(project.FullPath, name + ext);
      File.WriteAllText(path, defaultContent, Encoding.UTF8);
      var test = new FileVm(project.Suite, project, path);
      project.Children.Add(test);
      client.Send(new ClientMessage.FileLoaded(project.Id, test.FullPath, test.Id, test.Version));
      return test;
    }

    private void CopyReflectionText(object sender, RoutedEventArgs e)
    {
      var reflectionStruct = _reflectionTreeView.SelectedItem as ParseTreeReflectionStruct;
      if (reflectionStruct != null)
        CopyTreeNodeToClipboard(reflectionStruct.Description);
    }

    private void DeleteFile_MenuItem_OnClick(object sender, RoutedEventArgs e)
    {
      Delete();
    }

    private void OnAttachDebuggerClick(object sender, RoutedEventArgs e)
    {
      var currentSuite = ViewModel.CurrentSuite;
      if (currentSuite != null) {
        currentSuite.Client.Send(ClientMessage.AttachDebugger._N_constant_object);
      }
    }

    private void Delete()
    {
      NitraClient client = ViewModel.CurrentSuite.Client;
      if (client == null)
        return;

      var project = ViewModel.CurrentProject;
      var file    = ViewModel.CurrentFile;

      if (file != null)
      {
        var goldPath = Path.ChangeExtension(file.GoldFullPath, ".gold");

        if (File.Exists(file.FullPath))
           File.Delete(file.FullPath);

        if (File.Exists(goldPath))
          File.Delete(goldPath);

        var index = file.Project.Children.IndexOf(file);
        file.Project.Children.Remove(file);
        if (index < file.Project.Children.Count)
          file.Project.Children[index].IsSelected = true;
        else if (index > 0)
          file.Project.Children[index - 1].IsSelected = true;

        client.Send(new ClientMessage.FileUnloaded(file.Id));

        return;
      }

      if (project != null)
      {
        client.Send(new ClientMessage.FileUnloaded(file.Id));

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

    void _testsTreeView_TreeViewItem_KeyDown(object sender, KeyEventArgs e)
    {
      var elemtvi = (FrameworkElement)sender;
      var file = elemtvi.DataContext as FileVm;
      if (file != null)
      {
        if (Keyboard.Modifiers == ModifierKeys.None)
        {
          if (e.Key == Key.Enter || e.Key == Key.Up || e.Key == Key.Down)
          {
            if (file.IsEditing)
            {
              file.IsEditing = false;
            }
          }
          if (e.Key == Key.F2)
          {
            if (!file.IsEditing)
            {
              file.IsEditing = true;
              elemtvi.Focus();
            }
          }
        }
      }
    }

    void _testsTreeView_FileVm_TextBox_LostFocus(object sender, RoutedEventArgs e)
    {
      var elem = (FrameworkElement)sender;
      var file = elem.DataContext as FileVm;
      if (file != null)
      {
        if (file.IsEditing)
          file.IsEditing = false;
      }
    }

    public Color ColorFromArgb(int argb)
    {
      unchecked
      {
        return Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
      }
    }

    object IViewFor.ViewModel
    {
      get { return ViewModel; }
      set { ViewModel = (MainWindowViewModel) value; }
    }

    public MainWindowViewModel ViewModel { get; set; }

    private void ContextMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
      var menu = ((StackPanel)sender).ContextMenu;
      var item = (MenuItem)menu.Items[0];

      var project = ViewModel.CurrentProject;
      if (project == null)
        return;
      var exts = GetFileExtensions(project);

      item.Items.Clear();
      item.Tag = null;

      if (exts.Length == 0)
        item.IsEnabled = false;
      else if (exts.Length == 1)
      {
        item.Header = "_Add '" + exts[0] + "' file";
        item.Tag = exts[0];
      }
      else
      {
        foreach (var ext in exts)
        {
          item.Header = "_Add file";
          var subItem = new MenuItem { Header = ext };
          subItem.Tag = ext;
          subItem.Click += AddFile_MenuItem_OnClick;
          item.Items.Add(subItem);
        }
      }
    }

    private void CloneFile_MenuItem_OnClick(object sender, RoutedEventArgs e)
    {
      var project = ViewModel.CurrentProject;
      if (project == null)
        return;

      var file = ViewModel.CurrentFile;

      if (file != null)
      {
        var fullPath       = file.FullPath;
        var ext            = Path.GetExtension(fullPath);
        var defaultContent = File.ReadAllText(fullPath, Encoding.UTF8);
        var newFile        = AddNewFileToMultitest(ext, project, defaultContent);

        if (File.Exists(file.Gold))
          File.Copy(file.GoldFullPath, newFile.GoldFullPath);

        newFile.IsSelected = true;
        return;
      }
    }

    private void AddFile_MenuItem_OnClick(object sender, RoutedEventArgs e)
    {
      var item = (MenuItem)sender;
      var ext = (string)item.Tag;

      var project = ViewModel.CurrentProject;
      if (project == null)
        return;


      AddNewFileToMultitest(ext, project, Environment.NewLine).IsSelected = true;
      e.Handled = true;
    }

    private static ImmutableArray<string> GetFileExtensions(ProjectVm project)
    {
      NitraClient client = project.Solution.Suite.Client;
      client.Send(new ClientMessage.GetFileExtensions(project.Id, ImmutableArray.Create<string>()));
      var msg = client.Receive<ServerMessage.FileExtensions>();
      return msg.fileExtensions;
    }
  }
}
