using ICSharpCode.AvalonEdit.Highlighting;

using Nitra.ClientServer.Messages;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Nitra.Visualizer
{
  public partial class MainWindow
  {
    readonly Dictionary<int, HighlightingColor> _highlightingStyles = new Dictionary<int, HighlightingColor>();
    ImmutableArray<SpanInfo> _spanInfos = ImmutableArray<SpanInfo>.Empty;

    private void textBox1_HighlightLine(object sender, HighlightLineEventArgs e)
    {
      try
      {
        var line = e.Line;
        var spans = _spanInfos;

        foreach (var span in spans)
        {
          var start = line.Offset;
          var end   = line.Offset + line.Length;
          if (start > span.Span.EndPos || end < span.Span.StartPos)
            continue;
          
          var spanClassId = span.SpanClassId;
          var color = _highlightingStyles[spanClassId];
          var startOffset = Math.Max(line.Offset, span.Span.StartPos);
          var endOffset = Math.Min(line.EndOffset, span.Span.EndPos);
          var section = new HighlightedSection
          {
            Offset = startOffset,
            Length = endOffset - startOffset,
            Color = color
          };
          e.Sections.Add(section);
        }
      }
      catch (Exception ex) { Debug.WriteLine(ex.GetType().Name + ":" + ex.Message); }
    }
    
    private void UpdateSpanInfos(ServerMessage.KeywordHighlightingCreated keywordHighlighting)
    {
      _spanInfos = keywordHighlighting.spanInfos;
      _text.TextArea.TextView.Redraw();
    }

    private void UpdateHighlightingStyles(ServerMessage.LanguageLoaded languageInfo)
    {
      foreach (var spanClassInfo in languageInfo.spanClassInfos)
        _highlightingStyles[spanClassInfo.Id] = 
          new HighlightingColor
            {
              Foreground = new SimpleHighlightingBrush(ColorFromArgb(spanClassInfo.ForegroundColor))
            };
    }
    
    private void ResetHighlightingStyles()
    {
      _highlightingStyles.Clear();
    }
  }
}
