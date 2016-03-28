using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Document;
using System.Diagnostics;
using Nitra.ClientServer.Messages;
using System.Collections.Immutable;

namespace Nitra.Visualizer
{
  public sealed class NitraFoldingStrategy : AbstractFoldingStrategy
  {
    public TimeSpan TimeSpan { get; private set; }
    public ImmutableArray<OutliningInfo> Outlining { get; set; }

    public override IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
      try
      {
        var timer = Stopwatch.StartNew();
        TimeSpan = timer.Elapsed;

        var result = new List<NewFolding>();
        foreach (var o in Outlining)
        {
          var newFolding = new NewFolding
          {
            DefaultClosed = o.IsDefaultCollapsed,
            StartOffset   = o.Span.StartPos,
            EndOffset     = o.Span.EndPos
          };
          result.Add(newFolding);
        }
        result.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));

        firstErrorOffset = 0;
        return result;
      }
      catch (Exception ex)
      {
        Debug.WriteLine(ex.GetType().Name + ":" + ex.Message);
        firstErrorOffset = 0;
        return Enumerable.Empty<NewFolding>();
      }
    }
  }
}
