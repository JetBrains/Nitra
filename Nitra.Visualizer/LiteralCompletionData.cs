using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace Nitra.Visualizer
{
  class LiteralCompletionData : ICompletionData
  {
    public NSpan Span { get; private set; }

    public LiteralCompletionData(NSpan span, string text)
    {
      this.Span = span;
      this.Text = text;
    }

    public System.Windows.Media.ImageSource Image
    {
      get { return null; }
    }

    public string Text { get; private set; }

    // Use this property if you want to show a fancy UIElement in the list.
    public object Content
    {
      get { return this.Text; }
    }

    public object Description
    {
      get { return "Description for " + this.Text; }
    }

    public double Priority
    {
      get { return 0.0; }
    }

    public void Complete(TextArea textArea, ISegment completionSegment,
        EventArgs insertionRequestEventArgs)
    {
      var line = textArea.Document.GetLineByOffset(Span.EndPos);
      var lineText = textArea.Document.GetText(line);
      var end = Span.EndPos - line.Offset;
      for (; end < lineText.Length; end++)
      {
        var ch = lineText[end];
        if (!char.IsLetterOrDigit(ch))
          break;
      }
      textArea.Document.Replace(Span.StartPos, end + line.Offset - Span.StartPos, this.Text);
    }
  }
}
