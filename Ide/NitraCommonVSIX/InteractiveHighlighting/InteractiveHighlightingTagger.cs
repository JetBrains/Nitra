using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Nitra.ClientServer.Messages;
using System.Collections.Immutable;
using static Nitra.ClientServer.Messages.AsyncServerMessage;
using Nitra.VisualStudio.Models;

namespace Nitra.VisualStudio.BraceMatching
{
    /// <summary>
    /// 
    /// </summary>
  public class InteractiveHighlightingTagger : ITagger<TextMarkerTag>
  {
             TextViewModel  _textViewModelOpt;
    readonly ITextView      _textView;
    readonly ITextBuffer    _textBuffer;
             SnapshotPoint? _caretPosOpt;

    public event EventHandler<SnapshotSpanEventArgs>  TagsChanged;

    public InteractiveHighlightingTagger(ITextView textView, ITextBuffer textBuffer)
    {
      _textView    = textView;
      _textBuffer  = textBuffer;
      _caretPosOpt = null;

      _textView.Caret.PositionChanged += CaretPositionChanged;
      _textView.LayoutChanged         += ViewLayoutChanged;

      UpdateAtCaretPosition(_textView.Caret.Position);
    }

    internal TextViewModel TextViewModel
    {
      get
      {
        if (_textViewModelOpt == null)
        {
          if (!_textView.Properties.ContainsProperty(Constants.TextViewModelKey))
            return null;

          _textViewModelOpt = (TextViewModel)_textView.Properties.GetProperty(Constants.TextViewModelKey);
        }
        return _textViewModelOpt;
      }
    }

    void ViewLayoutChanged(object source, TextViewLayoutChangedEventArgs e)
    {
      if (e.NewSnapshot != e.OldSnapshot) //make sure that there has really been a change
        UpdateAtCaretPosition(_textView.Caret.Position);
    }

    void CaretPositionChanged(object _, CaretPositionChangedEventArgs e)
    {
      UpdateAtCaretPosition(e.NewPosition);
    }

    void UpdateAtCaretPosition(CaretPosition caretPosition)
    {
      if (TextViewModel == null)
        return;

      _caretPosOpt = caretPosition.Point.GetPoint(_textBuffer, caretPosition.Affinity);

      if (_caretPosOpt.HasValue)
      {
        var fileModel = TextViewModel.FileModel;
        var pos = _caretPosOpt.Value;
        fileModel.Server.CaretPositionChanged(fileModel.Id, pos.Position, pos.Snapshot.Version.VersionNumber - 1);
      }
      else
        TextViewModel.Reset();
    }

    public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      if (TextViewModel == null)
        yield break;

      var matchedBrackets = TextViewModel.MatchedBrackets;

      if (!_caretPosOpt.HasValue)
        yield break;

      var caretPos        = _caretPosOpt.Value;
      var lastSnapshot    = caretPos.Snapshot;
      var currentSnapshot = _textBuffer.CurrentSnapshot;

      if (matchedBrackets != null && lastSnapshot.Version.VersionNumber == matchedBrackets.Version + 1)
      {
        var tagName = "blue";
        foreach (MatchBrackets pair in matchedBrackets.results)
        {
          yield return MakeTagSpan(lastSnapshot, currentSnapshot, pair.Open, tagName);
          yield return MakeTagSpan(lastSnapshot, currentSnapshot, pair.Close, tagName);
          tagName = Constants.BraceMatchingSecond;
        }
      }

      var findSymbolReferences = TextViewModel.FindSymbolReferences;
      var id = TextViewModel.FileModel.Id;

      if (findSymbolReferences != null && lastSnapshot.Version.VersionNumber == findSymbolReferences.Version + 1)
      {
        foreach (var symbolRefs in findSymbolReferences.symbols)
        {
          foreach (var definition in symbolRefs.Definitions)
          {
            var loc = definition.Location;
            if (loc.File.FileId != id)
              continue;
            yield return MakeTagSpan(lastSnapshot, currentSnapshot, loc.Span, Constants.CurrentSymbol);
          }

          foreach (var fileEntries in symbolRefs.References)
          {
            if (fileEntries.File.FileId != id)
              continue;
            foreach (var span in fileEntries.Spans)
              yield return MakeTagSpan(lastSnapshot, currentSnapshot, span, Constants.CurrentSymbol);
          }
        }
      }
    }

    public static TagSpan<TextMarkerTag> MakeTagSpan(ITextSnapshot lastSnapshot, ITextSnapshot currentSnapshot, NSpan nSpan, string tagType)
    {
      var span           = new SnapshotSpan(lastSnapshot, VsUtils.Convert(nSpan));
      var translatedSpan = span.TranslateTo(currentSnapshot, SpanTrackingMode.EdgeExclusive);
      return new TagSpan<TextMarkerTag>(span, new TextMarkerTag(tagType));
    }

    internal void Update()
    {
      TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, _textBuffer.CurrentSnapshot.Length)));
    }
  }
}
