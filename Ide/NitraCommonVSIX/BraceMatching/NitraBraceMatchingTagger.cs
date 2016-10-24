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

namespace Nitra.VisualStudio.BraceMatching
{
  public class NitraBraceMatchingTagger : ITagger<TextMarkerTag>
  {
             ITextView       _textView;
             ITextBuffer     _textBuffer;
             SnapshotPoint?  _caretPosOpt;

    public event EventHandler<SnapshotSpanEventArgs>  TagsChanged;

    public NitraBraceMatchingTagger(ITextView textView, ITextBuffer textBuffer)
    {
      _textView    = textView;
      _textBuffer  = textBuffer;
      _caretPosOpt = null;
      _id          = textBuffer.Properties.GetProperty<int>(Constants.FileIdKey);
      _server      = (Server)_textBuffer.Properties.GetProperty(Constants.ServerKey);

      _textView.Caret.PositionChanged += CaretPositionChanged;
      _textView.LayoutChanged         += ViewLayoutChanged;

      //var path = sourceBuffer.GetFilePath();
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
      _caretPosOpt = caretPosition.Point.GetPoint(_textBuffer, caretPosition.Affinity);

      if (_caretPosOpt.HasValue)
      {
        var pos = _caretPosOpt.Value;
        _server.CaretPositionChanged(_id, pos.Position, pos.Snapshot.Version.VersionNumber - 1);
      }
      else
        _matchedBrackets = null;
    }

    public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      var matchedBrackets = _matchedBrackets;

      if (!_caretPosOpt.HasValue || matchedBrackets == null)
        yield break;

      var caretPos        = _caretPosOpt.Value;
      var lastSnapshot    = caretPos.Snapshot;
      var currentSnapshot = _textBuffer.CurrentSnapshot;

      if (lastSnapshot.Version.VersionNumber != matchedBrackets.Version + 1)
        yield break;

      var tagName = "blue";
      foreach (MatchBrackets pair in matchedBrackets.results)
      {
        yield return MakeTagSpan(lastSnapshot, currentSnapshot, pair.Open,  tagName);
        yield return MakeTagSpan(lastSnapshot, currentSnapshot, pair.Close, tagName);
        tagName = Constants.BraceMatchingSecond;
      }
    }

    public static TagSpan<TextMarkerTag> MakeTagSpan(ITextSnapshot lastSnapshot, ITextSnapshot currentSnapshot, NSpan nSpan, string tagType)
    {
      var span           = new SnapshotSpan(lastSnapshot, VsUtils.Convert(nSpan));
      var translatedSpan = span.TranslateTo(currentSnapshot, SpanTrackingMode.EdgeExclusive);
      return new TagSpan<TextMarkerTag>(span, new TextMarkerTag(tagType));
    }

    internal void Update(AsyncServerMessage.MatchedBrackets matchedBrackets)
    {
      _matchedBrackets = matchedBrackets;
      TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, _textBuffer.CurrentSnapshot.Length)));
    }
  }
}
