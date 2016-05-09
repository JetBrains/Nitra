using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using Nitra.ClientServer.Messages;
using Nitra.Visualizer.Infrastructure;
using Nitra.Visualizer.ViewModels;
using ReactiveUI;

namespace Nitra.Visualizer.Views
{
  public class NitraTextEditor : TextEditor, IViewFor<NitraTextEditorViewModel>
  {
    private IntelliSensePopup _popup;

    public NitraTextEditor()
    {
      SyntaxHighlighting = new StubHighlightingDefinition();
      TextArea.DefaultInputHandler.NestedInputHandlers.Add(new NitraSearchInputHandler(TextArea));
      
      this.WhenActivated(disposables => {
        _popup = new IntelliSensePopup(ViewModel.IntelliSensePopup);

        AddVisualChild(_popup);
        
        this.OneWayBind(ViewModel, vm => vm.IntelliSensePopup, v => v._popup.ViewModel)
            .AddTo(disposables);

        this.WhenAnyValue(vm => vm.ViewModel.Selection)
            .Subscribe(span => {
              if (span.HasValue)
                Select(span.Value.StartPos, span.Value.Length);
            })
            .AddTo(disposables);

        this.WhenAnyValue(vm => vm.ViewModel.ScrollPosition)
            .Where(pos => pos != null)
            .Subscribe(scrollPos => ScrollTo(scrollPos.Line, scrollPos.Column))
            .AddTo(disposables);

        ViewModel.WhenAnyValue(vm => vm.IntelliSensePopup.IsVisible)
                 .Where(popupVisible => popupVisible)
                 .Do(_ => UpdatePopupOffset())
                 .Subscribe(visible => _popup.List.Focus())
                 .AddTo(disposables);

        ViewModel.WhenAnyValue(vm => vm.IntelliSensePopup.IsVisible)
                 .Where(popupVisible => !popupVisible)
                 .Subscribe(visible => TextArea.Focus())
                 .AddTo(disposables);

        TextArea.SelectionChanged += (sender, args) => {
          var selection = TextArea.Selection;
          var span = selection.Segments.FirstOrDefault();
          
          if (span != null)
            ViewModel.Selection = new NSpan(span.StartOffset, span.EndOffset);
          else
            ViewModel.Selection = null;
        };

        TextArea.Caret.PositionChanged += (sender, args) => {
          ViewModel.CaretOffset = CaretOffset;
          ViewModel.CaretLine = TextArea.Caret.Line;
          ViewModel.CaretColumn = TextArea.Caret.Column;
        };
      });
    }

    private void UpdatePopupOffset()
    {
      var pos = TextArea.TextView.GetVisualPosition(TextArea.Caret.Position, VisualYPosition.LineBottom);
      _popup.HorizontalOffset = pos.X;
      _popup.VerticalOffset = pos.Y - (ActualHeight + VerticalOffset);
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

    object IViewFor.ViewModel
    {
        get { return ViewModel; }
        set { ViewModel = (NitraTextEditorViewModel) value; }
    }

    public NitraTextEditorViewModel ViewModel { get; set; }
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

    public SolidColorBrush Brush { get { return brush; } }
  }
}
