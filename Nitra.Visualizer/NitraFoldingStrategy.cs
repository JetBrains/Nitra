using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Document;
using Nitra.Internal;
using System.Diagnostics;

namespace Nitra.Visualizer
{
  public sealed class NitraFoldingStrategy : AbstractFoldingStrategy
  {
    public TimeSpan TimeSpan { get; private set; }

    public override IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
      var parseResult = ParseResult;
      if (parseResult == null)
      {
        firstErrorOffset = 0;
        return Enumerable.Empty<NewFolding>();
      }

      try
      {//parseResult.SourceSnapshot
        var timer = Stopwatch.StartNew();
        var outlining = new List<OutliningInfo>();
        parseResult.GetOutlining(outlining);
        TimeSpan = timer.Elapsed;

        var result = new List<NewFolding>();
        foreach (var o in outlining)
        {
          var newFolding = new NewFolding
          {
            DefaultClosed = o.IsDefaultCollapsed,
            StartOffset = o.Span.StartPos,
            EndOffset = o.Span.EndPos
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

    public IParseResult ParseResult { get; set; }
  }
}
