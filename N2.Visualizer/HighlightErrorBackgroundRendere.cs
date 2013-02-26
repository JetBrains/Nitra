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
      set { _errorPos = value; }
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
      //_editor.Document.Rec
      var segment = new Segment { Offset = _errorPos, EndOffset = _errorPos + 1 }; //GetLineByOffset(_errorPos);
      foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
      {
        drawingContext.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0xFF)), null,
            new Rect(rect.Location, new Size(Math.Max(textView.ActualWidth - 32, 0), rect.Height)));
      }
    }
  }

  class Segment : ISegment
  {

    public int EndOffset
    {
      get;
      set;
    }

    public int Length
    {
      get { return EndOffset - Offset; }
    }

    public int Offset
    {
      get;
      set;
    }
  }

}
