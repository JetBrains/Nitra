using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Nitra.ClientServer.Messages.AsyncServerMessage;
using Microsoft.VisualStudio.Text;
using System.Windows.Threading;
using Nitra.ClientServer.Messages;
using System.Collections.Immutable;
using Nitra.VisualStudio.Highlighting;
using System.Diagnostics;
using Nitra.VisualStudio.CompilerMessages;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Windows.Media;

namespace Nitra.VisualStudio.Models
{
  class FileModel : IDisposable
  {
    public const int KindCount = 3;
    readonly ITextBuffer                             _textBuffer;
    public   Server                                  Server                     { get; }
    public   FileId                                  Id                         { get; }
    public   IVsHierarchy                            Hierarchy                  { get; }
    public   string                                  FullPath                   { get; }
    public   CompilerMessage[][]                     CompilerMessages           { get; private set; }
    public   ITextSnapshot[]                         CompilerMessagesSnapshots  { get; private set; }
             ErrorListProvider[]                     _errorListProviders;

    readonly Dictionary<IWpfTextView, TextViewModel> _textViewModelsMap = new Dictionary<IWpfTextView, TextViewModel>();
             TextViewModel                           _activeTextViewModelOpt;
             TextViewModel                           _mouseHoverTextViewModelOpt;
             bool                                    _fileIsRemoved;
    
    public FileModel(FileId id, ITextBuffer textBuffer, Server server, Dispatcher dispatcher, IVsHierarchy hierarchy, string fullPath)
    {
      Hierarchy = hierarchy;
      FullPath = fullPath;
      Id = id;
      Server = server;
      _textBuffer = textBuffer;

      var snapshot = textBuffer.CurrentSnapshot;
      var empty = new CompilerMessage[0];
      CompilerMessages = new CompilerMessage[KindCount][] { empty, empty, empty };
      CompilerMessagesSnapshots = new ITextSnapshot[KindCount] { snapshot, snapshot, snapshot };
      _errorListProviders = new ErrorListProvider[KindCount] { null, null, null };

      server.Client.ResponseMap[id] = msg => dispatcher.BeginInvoke(DispatcherPriority.Normal,
        new Action<AsyncServerMessage>(msg2 => Response(msg2)), msg);

      server.Add(this);

      Activate();

      textBuffer.Changed += TextBuffer_Changed;
    }

    public void CaretPositionChanged(int position, FileVersion fileVersion)
    {
      var server = this.Server;

      if (server.IsLoaded)
        server.Client.Send(new ClientMessage.SetCaretPos(Id, fileVersion, position));
    }

    public void Activate()
    {
      var server = this.Server;

      if (server.IsLoaded)
        server.Client.Send(new ClientMessage.FileActivated(Id));
    }

    public TextViewModel GetOrAdd(IWpfTextView wpfTextView)
    {
      TextViewModel textViewModel;

      if (!_textViewModelsMap.TryGetValue(wpfTextView, out textViewModel))
        _textViewModelsMap.Add(wpfTextView, textViewModel = new TextViewModel(wpfTextView, this));

      return textViewModel;
    }

    public void Remove(IWpfTextView wpfTextView)
    {
      TextViewModel textViewModel;
      if (_textViewModelsMap.TryGetValue(wpfTextView, out textViewModel))
      {
        if (textViewModel == _activeTextViewModelOpt)
          _activeTextViewModelOpt = null;
        if (textViewModel == _mouseHoverTextViewModelOpt)
          _mouseHoverTextViewModelOpt = null;
        textViewModel.Dispose();
        _textViewModelsMap.Remove(wpfTextView);
      }

      return;
    }

    internal void ViewActivated(TextViewModel textViewModel)
    {
      _activeTextViewModelOpt = textViewModel;
    }

    internal void OnMouseHover(TextViewModel textViewModel)
    {
      _mouseHoverTextViewModelOpt = textViewModel;
    }

    public void Dispose()
    {
      _textBuffer.Changed -= TextBuffer_Changed;
      foreach (var errorListProvider in _errorListProviders)
        if (errorListProvider != null)
          errorListProvider.Dispose();
      _errorListProviders = null;
      var client = Server.Client;
      Action<AsyncServerMessage> value;
      client.ResponseMap.TryRemove(Id, out value);
      if (!_fileIsRemoved)
        client.Send(new ClientMessage.FileDeactivated(Id));
      _textBuffer.Properties.RemoveProperty(Constants.FileModelKey);
      Server.Remove(this);
    }

    void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
    {
      var textBuffer = (ITextBuffer)sender;
      var newVersion = e.AfterVersion.Convert();
      var fileModel = textBuffer.Properties.GetProperty<FileModel>(Constants.FileModelKey);
      var id = fileModel.Id;
      var changes = e.Changes;

      if (changes.Count == 1)
        Server.Client.Send(new ClientMessage.FileChanged(id, newVersion, VsUtils.Convert(changes[0])));
      else
      {
        var builder = ImmutableArray.CreateBuilder<FileChange>(changes.Count);

        foreach (var change in changes)
          builder.Add(VsUtils.Convert(change));

        Server.Client.Send(new ClientMessage.FileChangedBatch(id, newVersion, builder.MoveToImmutable()));
      }
    }

    internal void Remove()
    {
      _fileIsRemoved = true;
      Server.Client.Send(new ClientMessage.FileUnloaded(Id));
    }

    void Response(AsyncServerMessage msg)
    {
      Debug.Assert(msg.FileId >= 0);
      ITextBuffer textBuffer = _textBuffer;

      OutliningCreated                outlining;
      KeywordsHighlightingCreated     keywordHighlighting;
      SymbolsHighlightingCreated      symbolsHighlighting;
      MatchedBrackets                 matchedBrackets;
      MappingMessages                 mappingMessages;
      ParsingMessages                 parsingMessages;
      SemanticAnalysisMessages        semanticAnalysisMessages;
      FindSymbolReferences            findSymbolReferences;
      Hint                            hint;

      if ((outlining = msg as OutliningCreated) != null)
      {
        var tegget = (OutliningTagger)textBuffer.Properties.GetProperty(Constants.OutliningTaggerKey);
        tegget.Update(outlining);
      }
      else if ((keywordHighlighting = msg as KeywordsHighlightingCreated) != null)
        UpdateSpanInfos(HighlightingType.Keyword, keywordHighlighting.spanInfos, keywordHighlighting.Version);
      else if ((symbolsHighlighting = msg as SymbolsHighlightingCreated) != null)
        UpdateSpanInfos(HighlightingType.Symbol, symbolsHighlighting.spanInfos, symbolsHighlighting.Version);
      else if ((matchedBrackets = msg as MatchedBrackets) != null)
      {
        if (_activeTextViewModelOpt == null)
          return;

        _activeTextViewModelOpt.Update(matchedBrackets);
      }
      else if ((findSymbolReferences = msg as FindSymbolReferences) != null)
      {
        if (_activeTextViewModelOpt == null)
          return;

        _activeTextViewModelOpt.Update(findSymbolReferences);
      }
      else if ((parsingMessages = msg as ParsingMessages) != null)
        UpdateCompilerMessages(0, parsingMessages.messages, parsingMessages.Version);
      else if ((mappingMessages = msg as MappingMessages) != null)
        UpdateCompilerMessages(1, mappingMessages.messages, mappingMessages.Version);
      else if ((semanticAnalysisMessages = msg as SemanticAnalysisMessages) != null)
        UpdateCompilerMessages(2, semanticAnalysisMessages.messages, semanticAnalysisMessages.Version);
      else if ((hint = msg as Hint) != null)
        _mouseHoverTextViewModelOpt?.ShowHint(hint);
    }

    internal Brush SpanClassToBrush(string spanClass, IWpfTextView _wpfTextView)
    {
      var classifierOpt = GetClassifierOpt();
      if (classifierOpt == null)
        return Server.SpanClassToBrush(spanClass);

      return classifierOpt.SpanClassToBrush(spanClass, _wpfTextView);
    }

    private void UpdateCompilerMessages(int index, CompilerMessage[] messages, int version)
    {
      var snapshot = _textBuffer.CurrentSnapshot;

      if (snapshot.Version.VersionNumber != version + 1)
        return;

      CompilerMessages[index]          = messages;
      CompilerMessagesSnapshots[index] = snapshot;

      CompilerMessagesTagger tegger;
      if (_textBuffer.Properties.TryGetProperty<CompilerMessagesTagger>(Constants.CompilerMessagesTaggerKey, out tegger))
        tegger.Update();

      var errorListProvider = _errorListProviders[index];
      var noTasks = errorListProvider == null || errorListProvider.Tasks.Count == 0;
      if (!(messages.Length == 0 && noTasks))
      {
        if (errorListProvider == null)
          _errorListProviders[index] = errorListProvider = new ErrorListProvider(Server.ServiceProvider);
        errorListProvider.SuspendRefresh();
        try
        {
          var tasks = errorListProvider.Tasks;
          tasks.Clear();
          foreach (var msg in messages)
          {
            var startPos = msg.Location.Span.StartPos;
            if (startPos > snapshot.Length)
            {
              continue;
            }
            var line = snapshot.GetLineFromPosition(startPos);
            var col = startPos - line.Start.Position;
            var task = new ErrorTask()
            {
              Text = msg.Text,
              Category = TaskCategory.CodeSense,
              ErrorCategory = ConvertMessageType(msg.Type),
              Priority = TaskPriority.High,
              HierarchyItem = Hierarchy,
              Line = line.LineNumber,
              Column = col,
              Document = FullPath,
            };

            task.Navigate += Task_Navigate;

            errorListProvider.Tasks.Add(task);
          }
        }
        finally { errorListProvider.ResumeRefresh(); }
      }
    }

    TextViewModel GetTextViewModel()
    {
      if (_activeTextViewModelOpt != null)
        return _activeTextViewModelOpt;

      foreach (TextViewModel textViewModel in _textViewModelsMap.Values)
        return textViewModel;

      return null;
    }

    private void Task_Navigate(object sender, EventArgs e)
    {
      var task = (ErrorTask)sender;


      var textViewModel = GetTextViewModel();
      if (textViewModel == null)
      {
        VsUtils.NavigateTo(Server.ServiceProvider, FullPath, task.Line, task.Column);
        return;
      }

      textViewModel.Navigate(task.Line, task.Column);
    }

    static TaskErrorCategory ConvertMessageType(CompilerMessageType type)
    {
      switch (type)
      {
        case CompilerMessageType.FatalError:
          return TaskErrorCategory.Error;
        case CompilerMessageType.Error:
          return TaskErrorCategory.Error;
        case CompilerMessageType.Warning:
          return TaskErrorCategory.Warning;
        case CompilerMessageType.Hint:
          return TaskErrorCategory.Message;
        default:
          return TaskErrorCategory.Error;
      }
    }

    NitraEditorClassifier GetClassifierOpt()
    {
      NitraEditorClassifier classifier;
      _textBuffer.Properties.TryGetProperty(Constants.NitraEditorClassifierKey, out classifier);
      return classifier;
    }

    void UpdateSpanInfos(HighlightingType highlightingType, ImmutableArray<SpanInfo> spanInfos, FileVersion version)
    {
      var classifierOpt = GetClassifierOpt();
      if (classifierOpt == null)
        return;
      classifierOpt.Update(highlightingType, spanInfos, version);
    }
  }
}
