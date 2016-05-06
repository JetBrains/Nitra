using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using Nitra.Visualizer.ViewModels;
using ReactiveUI;

namespace Nitra.Visualizer.Views
{
  public class NitraTextEditor : TextEditor, IViewFor<NitraTextEditorViewModel>
  {
    private readonly Popup _popup = new Popup();
    private readonly ListView _popupList = new ListView();

    public NitraTextEditor()
    {
      SyntaxHighlighting = new StubHighlightingDefinition();
      TextArea.DefaultInputHandler.NestedInputHandlers.Add(new NitraSearchInputHandler(TextArea));

      TextArea.Caret.PositionChanged += (sender, args) => {
        ViewModel.CaretOffset = CaretOffset;
        ViewModel.CaretLine = TextArea.Caret.Line;
        ViewModel.CaretColumn = TextArea.Caret.Column;

        var p = TextArea.Caret.Position;
        var p2 = TextArea.TextView.GetVisualPosition(p, VisualYPosition.LineBottom);
        // вот здесь надо координаты установить, чтобы _popup был под кареткой
        // а по уму нужно брать координаты не каретки, а координаты текста для result.referenceSpan
        _popup.HorizontalOffset = p2.X;
        _popup.VerticalOffset = p2.Y - (ActualHeight + TextArea.TextView.ScrollOffset.Y);
      };

      _popup.HorizontalAlignment = HorizontalAlignment.Left;
      _popup.VerticalAlignment = VerticalAlignment.Top;
      _popup.StaysOpen = false;
      _popup.Child = _popupList;

      AddVisualChild(_popup);

      this.WhenActivated(d => {
        this.OneWayBind(ViewModel, vm => vm.PopupList, v => v._popupList.ItemsSource);

        this.Bind(ViewModel, vm => vm.PopupVisible, v => v._popup.IsOpen);
        this.Bind(ViewModel, vm => vm.SelectedPopupItem, v => v._popupList.SelectedItem);

        this.WhenAnyValue(vm => vm.ViewModel.Selection)
            .Subscribe(span => Select(span.StartPos, span.Length));

        this.WhenAnyValue(vm => vm.ViewModel.ScrollPosition)
            .Subscribe(scrollPos => ScrollTo(scrollPos.Line, scrollPos.Column));

        this.WhenAnyValue(vm => vm.ViewModel.PopupVisible)
            .Where(visible => visible)
            .Subscribe(visible => {
              if (visible) {
                _popupList.Focus();
                Debug.WriteLine("list focused");
              } else {
                TextArea.TextView.Focus();
                Debug.WriteLine("text view focused");
              }
            });

        //var events = this.Events();

        //events.PreviewMouseLeftButtonDown
        //      .InvokeCommand(ViewModel, vm => vm.SelectItem);

        //events.KeyDown
        //      .Where(a => a.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.None)
        //      .InvokeCommand(ViewModel, vm => vm.SelectItem);
      });
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
