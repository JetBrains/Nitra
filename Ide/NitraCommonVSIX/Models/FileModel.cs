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

namespace Nitra.VisualStudio.Models
{
  class FileModel : IDisposable
  {
    readonly ITextBuffer            _textBuffer;
    readonly Server                 _server;
    readonly int                    _id;
    readonly Dictionary<IWpfTextView, TextViewModel> _textViewModelsMap = new Dictionary<IWpfTextView, TextViewModel>();
    private Dispatcher dispatcher;

    public FileModel(int id, ITextBuffer textBuffer, Server server, Dispatcher dispatcher)
    {
      _id         = id;
      _textBuffer = textBuffer;
      _server     = server;

      server.Client.ResponseMap[id] = msg => dispatcher.BeginInvoke(DispatcherPriority.Normal,
        new Action<AsyncServerMessage>(msg2 => Response(msg2)), msg);

      server.Client.Send(new ClientMessage.FileActivated(id));
      textBuffer.Changed += TextBuffer_Changed;
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
        textViewModel.Dispose();
        _textViewModelsMap.Remove(wpfTextView);
      }

      if (_textViewModelsMap.Count == 0)
        Dispose();

      return;
    }

    public void Dispose()
    {
      _textBuffer.Changed -= TextBuffer_Changed;
      var client = _server.Client;
      Action<AsyncServerMessage> value;
      client.ResponseMap.TryRemove(_id, out value);
      client.Send(new ClientMessage.FileDeactivated(_id));
      _textBuffer.Properties.RemoveProperty(Constants.FileModelKey);
    }

    void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
    {
      var textBuffer = (ITextBuffer)sender;
      var newVersion = e.AfterVersion.VersionNumber - 1;
      var id = textBuffer.Properties.GetProperty<int>(Constants.FileIdKey);
      var changes = e.Changes;

      if (changes.Count == 1)
        _server.Client.Send(new ClientMessage.FileChanged(id, newVersion, VsUtils.Convert(changes[0])));
      else
      {
        var builder = ImmutableArray.CreateBuilder<FileChange>(changes.Count);

        foreach (var change in changes)
          builder.Add(VsUtils.Convert(change));

        _server.Client.Send(new ClientMessage.FileChangedBatch(id, newVersion, builder.MoveToImmutable()));
      }
    }

    void Response(AsyncServerMessage msg)
    {
      ITextBuffer textBuffer = _textBuffer;
      OutliningCreated outlining;
      KeywordsHighlightingCreated keywordHighlighting;
      SymbolsHighlightingCreated symbolsHighlighting;
      MatchedBrackets matchedBrackets;

      if ((outlining = msg as OutliningCreated) != null)
      {
        var tegget = (OutliningTagger)textBuffer.Properties.GetProperty(Constants.OutliningTaggerKey);
        tegget.Update(outlining);
      }
      else if ((keywordHighlighting = msg as KeywordsHighlightingCreated) != null)
        UpdateSpanInfos(textBuffer, HighlightingType.Keyword, keywordHighlighting.spanInfos, keywordHighlighting.Version);
      else if ((symbolsHighlighting = msg as SymbolsHighlightingCreated) != null)
        UpdateSpanInfos(textBuffer, HighlightingType.Symbol, symbolsHighlighting.spanInfos, symbolsHighlighting.Version);
      else if ((matchedBrackets = msg as MatchedBrackets) != null)
      {
        if (!textBuffer.Properties.ContainsProperty(Constants.BraceMatchingTaggerKey))
          return;
        var tegget = (NitraBraceMatchingTagger)textBuffer.Properties.GetProperty(Constants.BraceMatchingTaggerKey);
        tegget.Update(matchedBrackets);
      }
    }

    void UpdateSpanInfos(ITextBuffer textBuffer, HighlightingType highlightingType, ImmutableArray<SpanInfo> spanInfos, int version)
    {
      if (!textBuffer.Properties.ContainsProperty(Constants.NitraEditorClassifierKey))
        return;

      var tegget = (NitraEditorClassifier)textBuffer.Properties.GetProperty(Constants.NitraEditorClassifierKey);
      tegget.Update(highlightingType, spanInfos, version);
    }
  }
}
