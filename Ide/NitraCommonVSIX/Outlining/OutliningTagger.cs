using Nemerle;
using Nemerle.Collections;
using Nemerle.Utility;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Nitra.ClientServer.Messages;
using System.Collections.Immutable;

namespace Nitra.VisualStudio
{
  public class OutliningTagger : ITagger<IOutliningRegionTag>
  {
    readonly ITextBuffer                   _buffer;
             ITextSnapshot                 _oldSnapshot;
             ImmutableArray<OutliningInfo> _outlining;

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    public OutliningTagger(ITextBuffer buffer)
    {
      _buffer      = buffer;
      _oldSnapshot = buffer.CurrentSnapshot;
      _outlining   = ImmutableArray<OutliningInfo>.Empty;
    }

    public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      var oldSnapshot = _oldSnapshot;
      var outlining   = _outlining;

      foreach (var span in spans)
      {
        var translatedSnapshotSpan = span.TranslateTo(oldSnapshot, SpanTrackingMode.EdgeExclusive);
        var translatedSpan         = translatedSnapshotSpan.Span;
        var nSpan = new NSpan(translatedSpan.Start, translatedSpan.End);
        var info = new OutliningInfo(nSpan, false, false);
        // The outlining array is sorted. Use BinarySearch() to find first a span which intersects with the processing span.
        var index = outlining.BinarySearch(info, OutliningInfo.Comparer);
        if (index < 0)
          index = ~index; // no exact match
        OutliningInfo currentInfo;
        for (int i = index; i < outlining.Length && nSpan.IntersectsWith((currentInfo = outlining[i]).Span); i++)
        {
          var currentSpan = currentInfo.Span;
          var tagSpan = new TagSpan<IOutliningRegionTag>(
            new SnapshotSpan(oldSnapshot, new Span(currentSpan.StartPos, currentSpan.Length))
              .TranslateTo(span.Snapshot, SpanTrackingMode.EdgeExclusive),
            new OutliningRegionTag(currentInfo.IsDefaultCollapsed, currentInfo.IsImplementation, "...", "colapsed code..."));
          yield return tagSpan;
        }
      }
    }

    internal void Update(AsyncServerMessage.OutliningCreated outlining)
    {
      var snapshot = _buffer.CurrentSnapshot;

      if (snapshot.Version.VersionNumber != outlining.Version + 1)
        return;

      // TODO: Implement incremental update

      _oldSnapshot = snapshot;
      _outlining   = outlining.outlining;

      var span = new SnapshotSpan(snapshot, Span.FromBounds(0, snapshot.Length));
      FierTagsChanged(span);
    }

    void FierTagsChanged(SnapshotSpan span)
    {
      if (TagsChanged != null)
        TagsChanged(this, new SnapshotSpanEventArgs(span));
    }
  }
}
