﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Document;
using System.Windows.Media;

namespace Nitra.Visualizer
{
  public class NitraTextEditor : TextEditor
  {
    public NitraTextEditor()
    {
      SyntaxHighlighting = new StubHighlightingDefinition();
    }

    public event EventHandler<HighlightLineEventArgs> HighlightLine;

    private IList<HighlightedSection> OnHighlightLine(DocumentLine line)
    {
      var highlightLineHandler = HighlightLine;
      if (highlightLineHandler != null)
      {
        var args = new HighlightLineEventArgs(line);
        highlightLineHandler(this, args);
        return args.Sections;
      }
      return null;
    }

    protected override IVisualLineTransformer CreateColorizer(IHighlightingDefinition highlightingDefinition)
    {
      return new NitraHighlightingColorizer(this);
    }

    private sealed class NitraHighlightingColorizer : DocumentColorizingTransformer
    {
      public NitraHighlightingColorizer(NitraTextEditor textEditor)
      {
        _textEditor = textEditor;
      }

      private readonly NitraTextEditor _textEditor;

      protected override void ColorizeLine(DocumentLine line)
      {
        var sections = _textEditor.OnHighlightLine(line);
        if (sections != null)
          foreach (var section in sections)
            ChangeLinePart(section.Offset, section.Offset + section.Length, element => ApplyColorToElement(element, section.Color));
      }

      private void ApplyColorToElement(VisualLineElement element, HighlightingColor color)
      {
        if (color.Foreground != null)
        {
          Brush brush = color.Foreground.GetBrush(base.CurrentContext);
          if (brush != null)
          {
            element.TextRunProperties.SetForegroundBrush(brush);
          }
        }
        if (color.Background != null)
        {
          Brush brush2 = color.Background.GetBrush(base.CurrentContext);
          if (brush2 != null)
          {
            element.TextRunProperties.SetBackgroundBrush(brush2);
          }
        }
        if (color.FontStyle.HasValue || color.FontWeight.HasValue)
        {
          Typeface typeface = element.TextRunProperties.Typeface;
          element.TextRunProperties.SetTypeface(new Typeface(typeface.FontFamily, color.FontStyle ?? typeface.Style, color.FontWeight ?? typeface.Weight, typeface.Stretch));
        }
      }
    }

    private sealed class StubHighlightingDefinition : IHighlightingDefinition
    {
      public HighlightingColor GetNamedColor(string name)
      {
        throw new NotImplementedException();
      }

      public HighlightingRuleSet GetNamedRuleSet(string name)
      {
        throw new NotImplementedException();
      }

      public HighlightingRuleSet MainRuleSet
      {
        get { throw new NotImplementedException(); }
      }

      public string Name
      {
        get { throw new NotImplementedException(); }
      }

      public IEnumerable<HighlightingColor> NamedHighlightingColors
      {
        get { throw new NotImplementedException(); }
      }
    }
  }

  public sealed class HighlightLineEventArgs : EventArgs
  {
    public HighlightLineEventArgs(DocumentLine line)
    {
      Line = line;
      Sections = new List<HighlightedSection>();
    }

    public DocumentLine Line { get; private set; }

    public IList<HighlightedSection> Sections { get; private set; }
  }

  public sealed class SimpleHighlightingBrush : HighlightingBrush
  {
    private readonly SolidColorBrush brush;

    public SimpleHighlightingBrush(SolidColorBrush brush)
    {
      brush.Freeze();
      this.brush = brush;
    }

    public SimpleHighlightingBrush(Color color)
      : this(new SolidColorBrush(color))
    {
    }

    public override Brush GetBrush(ITextRunConstructionContext context)
    {
      return this.brush;
    }

    public override string ToString()
    {
      return this.brush.ToString();
    }
  }
}
