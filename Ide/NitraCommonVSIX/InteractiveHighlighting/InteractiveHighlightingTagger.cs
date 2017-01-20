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

    // don't cache it! Property can be changed in _textView.Properties when the view hide or show.
    TextViewModel GetTextViewModel() => (TextViewModel)_textView.Properties.GetProperty(Constants.TextViewModelKey);

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
      var textViewModel = GetTextViewModel();
      if (textViewModel == null)
        return;

      _caretPosOpt = caretPosition.Point.GetPoint(_textBuffer, caretPosition.Affinity);

      if (_caretPosOpt.HasValue)
      {
        var fileModel = textViewModel.FileModel;
        var pos = _caretPosOpt.Value;
        fileModel.CaretPositionChanged(pos.Position, pos.Snapshot.Version.Convert());
      }
      else
        textViewModel.Reset();
    }

    public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      var textViewModel = GetTextViewModel();

      if (textViewModel == null)
        yield break;

      var matchedBrackets = textViewModel.MatchedBrackets;

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

      var findSymbolReferences = textViewModel.FindSymbolReferences;
      var id = textViewModel.FileModel.Id;

      if (findSymbolReferences != null && lastSnapshot.Version.VersionNumber == findSymbolReferences.Version + 1)
      {
        foreach (var symbolRefs in findSymbolReferences.symbols)
        {
          foreach (var definition in symbolRefs.Definitions)
          {
            var loc = definition.Location;
            if (loc.File.FileId != id)
              continue;
            yield return MakeTagSpan(lastSnapshot, currentSnapshot, loc.Span, Constants.DefenitionHighlighting);
          }

          foreach (var fileEntries in symbolRefs.References)
          {
            if (fileEntries.File.FileId != id)
              continue;
            foreach (var range in fileEntries.Ranges)
              yield return MakeTagSpan(lastSnapshot, currentSnapshot, range.Span, Constants.ReferenceHighlighting);
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
