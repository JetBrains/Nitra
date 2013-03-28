using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Document;

namespace N2.Visualizer
{
  public sealed class N2FoldingStrategy : AbstractFoldingStrategy
  {
    public override IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
      var parseResult = ParseResult;
      if (parseResult == null)
      {
        firstErrorOffset = 0;
        return Enumerable.Empty<NewFolding>();
      }

      var outlining = new List<OutliningInfo>();
      parseResult.GetOutlining(outlining);

      var result = new List<NewFolding>();
      foreach (var o in outlining)
      {
        var newFolding = new NewFolding
        {
          DefaultClosed = false,
          StartOffset = o.Span.StartPos,
          EndOffset = o.Span.EndPos
        };
        result.Add(newFolding);
      }
      result.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));

      firstErrorOffset = 0;
      return result;
    }

    public ParseResult ParseResult { get; set; }
  }
}
