using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit;
using System.Windows.Media;
using System.Windows;
using ICSharpCode.AvalonEdit.Document;

namespace N2.Visualizer
{
  public class HighlightErrorBackgroundRendere : IBackgroundRenderer
  {
    private TextEditor _editor;
    private int _errorPos = -1;
    public int ErrorPos
    {
      get { return _errorPos; }
      set { _errorPos = value; /*_editor.TextView.Redraw();*/ }
    }

    public HighlightErrorBackgroundRendere(TextEditor editor)
    {
      _editor = editor;
    }

    public KnownLayer Layer
    {
      get { return KnownLayer.Caret; }
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
      if (_editor.Document == null || _errorPos < 0)
        return;

      textView.EnsureVisualLines();

      var segment = new Segment(_errorPos, _editor.Text.Length); //GetLineByOffset(_errorPos);

      foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
      {
        drawingContext.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(0x40, 255, 100, 100)), null,
            new Rect(rect.Location, new Size(Math.Max(textView.ActualWidth - 32, 0), rect.Height)));
      }
    }
  }

  class Segment : ISegment
  {
    public Segment(int startOffset, int endOffset)
    {
      Offset    = startOffset;
      EndOffset = endOffset;
    }

    public int Offset    { get; private set; }
    public int EndOffset { get; private set; }

    public int Length
    {
      get { return EndOffset - Offset; }
    }
  }
}
