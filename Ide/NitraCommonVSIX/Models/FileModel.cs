﻿using Microsoft.VisualStudio.Text.Editor;
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

namespace Nitra.VisualStudio.Models
{
  class FileModel : IDisposable
  {
    readonly ITextBuffer                             _textBuffer;
    public   Server                                  Server { get; }
    public   int                                     Id     { get; }
    readonly Dictionary<IWpfTextView, TextViewModel> _textViewModelsMap = new Dictionary<IWpfTextView, TextViewModel>();
             TextViewModel                           _activeTextViewModelOpt;
             Dispatcher                              _dispatcher;

    public FileModel(int id, ITextBuffer textBuffer, Server server, Dispatcher dispatcher)
    {
      Id          = id;
      Server      = server;
      _textBuffer = textBuffer;

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

    internal void ViewActivated(TextViewModel textViewModel)
    {
      _activeTextViewModelOpt = textViewModel;
    }

    public void Dispose()
    {
      _textBuffer.Changed -= TextBuffer_Changed;
      var client = Server.Client;
      Action<AsyncServerMessage> value;
      client.ResponseMap.TryRemove(Id, out value);
      client.Send(new ClientMessage.FileDeactivated(Id));
      _textBuffer.Properties.RemoveProperty(Constants.FileModelKey);
    }

    void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
    {
      var textBuffer = (ITextBuffer)sender;
      var newVersion = e.AfterVersion.VersionNumber - 1;
      var id = textBuffer.Properties.GetProperty<int>(Constants.FileIdKey);
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
        if (_activeTextViewModelOpt == null)
          return;

        _activeTextViewModelOpt.Update(matchedBrackets);
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