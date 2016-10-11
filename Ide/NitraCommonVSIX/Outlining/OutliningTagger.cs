using Nemerle;
using Nemerle.Collections;
using Nemerle.Utility;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Nitra.VisualStudio
{
  public class OutliningTagger : ITagger<IOutliningRegionTag>
  {
    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    ITextBuffer _buffer;

    public OutliningTagger(ITextBuffer buffer)
    {
      _buffer = buffer;
    }

    public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      yield break;
    }
  }
}
