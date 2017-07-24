using System;
using System.Windows.Markup;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Nitra.ClientServer.Messages;

namespace Nitra.Visualizer
{
  class CompletionData : ICompletionData, IComparable<CompletionData>
  {
    public NSpan  Span { get; private set; }
    public string Text { get; private set; }
    public object Content { get { return ParseXaml(ContentXaml); } }
    public string ContentXaml { get; private set; }
    public double Priority { get; private set; }
    public string DescriptionXaml { get; private set; }
    public object Description { get { return ParseXaml(DescriptionXaml); } }

    public CompletionData(NSpan span, string text, string contentXaml = null, string descriptionXaml = null, double priority = 0.0)
    {
      Span = span;
      ContentXaml = contentXaml ?? text;
      DescriptionXaml = descriptionXaml ?? "";
      Priority = priority;
      Text = text;
    }

    public System.Windows.Media.ImageSource Image
    {
      get { return null; }
    }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
      textArea.Document.Replace(Span.StartPos, Span.Length, this.Text);
    }

    public int CompareTo(CompletionData other)
    {
      return string.Compare(this.Text, other.Text, StringComparison.OrdinalIgnoreCase);
    }

    private object ParseXaml(string xaml)
    {
      return XamlReader.Parse(Utils.WrapToXaml(xaml));
    }
  }
}
