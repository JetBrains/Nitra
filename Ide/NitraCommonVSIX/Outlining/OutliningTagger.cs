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
      Debug.WriteLine($"GetTags({spans}) begin");
      var oldSnapshot = _oldSnapshot;
      var outlining   = _outlining;

      foreach (var span in spans)
      {
        var translatedSnapshotSpan = span.TranslateTo(oldSnapshot, SpanTrackingMode.EdgeExclusive);
        var translatedSpan         = translatedSnapshotSpan.Span;
        var nSpan                  = new NSpan(translatedSpan.Start, translatedSpan.End);
        var info                   = new OutliningInfo(nSpan, false, false);

        foreach (var currentInfo in outlining)
        {
          var currentSpan = currentInfo.Span;

          if (!nSpan.IntersectsWith(currentSpan))
            continue;

          var tagSpan = new TagSpan<IOutliningRegionTag>(
            new SnapshotSpan(oldSnapshot, new Span(currentSpan.StartPos, currentSpan.Length))
              .TranslateTo(span.Snapshot, SpanTrackingMode.EdgeExclusive),
            new OutliningRegionTag(currentInfo.IsDefaultCollapsed, currentInfo.IsImplementation, "...", "colapsed code..."));
          Debug.WriteLine($"  tagSpan={{Start={tagSpan.Span.Start.Position}, Len={tagSpan.Span.Length}}}");
          yield return tagSpan;
        }
        Debug.WriteLine($"GetTags({spans}) end");
      }
    }

    internal void Update(AsyncServerMessage.OutliningCreated outlining)
    {
      Debug.WriteLine($"Update(Length={outlining.outlining.Length}) begin");
      var snapshot = _buffer.CurrentSnapshot;

      if (snapshot.Version.VersionNumber != outlining.Version + 1)
        return;

      // TODO: Implement incremental update

      _oldSnapshot = snapshot;
      _outlining   = outlining.outlining;

      var span = new SnapshotSpan(snapshot, Span.FromBounds(0, snapshot.Length));
      FierTagsChanged(span);
      Debug.WriteLine($"Update(Length={outlining.outlining.Length}) end");
    }

    void FierTagsChanged(SnapshotSpan span)
    {
      if (TagsChanged != null)
        TagsChanged(this, new SnapshotSpanEventArgs(span));
    }
  }
}
