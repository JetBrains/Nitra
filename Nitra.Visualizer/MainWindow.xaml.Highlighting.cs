using ICSharpCode.AvalonEdit.Highlighting;

using Nitra.ClientServer.Messages;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Nitra.Visualizer.Views;
using System.Windows.Media;
using System.Windows;
using Nitra.Visualizer.ViewModels;

namespace Nitra.Visualizer
{
  public partial class MainWindow
  {
    public const int CurrentAstSpanClassId = -1;

    readonly Dictionary<int, HighlightingColor> _highlightingStyles = new Dictionary<int, HighlightingColor>();
    ImmutableArray<SpanInfo> _keywordsSpanInfos    = ImmutableArray<SpanInfo>.Empty;
    ImmutableArray<SpanInfo> _symbolsSpanInfos     = ImmutableArray<SpanInfo>.Empty;
    ImmutableArray<SpanInfo> _interactiveSpanInfos = ImmutableArray<SpanInfo>.Empty;

    private void textBox1_HighlightLine(object sender, HighlightLineEventArgs e)
    {
      try
      {
        HighlightLine(e, _keywordsSpanInfos);
        HighlightLine(e, _symbolsSpanInfos);
        HighlightLine(e, _interactiveSpanInfos);
      }
      catch (Exception ex) { Debug.WriteLine(ex.GetType().Name + ":" + ex.Message); }
    }

    private void HighlightLine(HighlightLineEventArgs e, ImmutableArray<SpanInfo> spans)
    {
      var line = e.Line;

      foreach (var span in spans)
      {
        var start = line.Offset;
        var end = line.Offset + line.Length;
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

    private void ClearHighlighting()
    {
      _keywordsSpanInfos    = ImmutableArray<SpanInfo>.Empty;
      _symbolsSpanInfos     = ImmutableArray<SpanInfo>.Empty;
      ClearAstHighlighting();
    }

    private void ClearAstHighlighting()
    {
      _interactiveSpanInfos = ImmutableArray<SpanInfo>.Empty;
    }

    private void UpdateKeywordSpanInfos(AsyncServerMessage.KeywordsHighlightingCreated keywordHighlighting)
    {
      _keywordsSpanInfos = keywordHighlighting.spanInfos;
      _textEditor.TextArea.TextView.Redraw();
    }

    private void UpdateSymbolsSpanInfos(AsyncServerMessage.SymbolsHighlightingCreated symbolsHighlighting)
    {
      _symbolsSpanInfos = symbolsHighlighting.spanInfos;
      _textEditor.TextArea.TextView.Redraw();
    }

    private void UpdateHighlightingStyles(AsyncServerMessage.LanguageLoaded languageInfo)
    {
      _highlightingStyles[CurrentAstSpanClassId] = new HighlightingColor { Background = new SimpleHighlightingBrush(Brushes.LightCyan) };

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

    private void _astTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
      var ast = e.NewValue as AstNodeViewModel;

      if (ast != null && !ast.Span.IsEmpty && !ast.IsRoot)
      {
        _interactiveSpanInfos = ImmutableArray.Create(new SpanInfo(ast.Span, CurrentAstSpanClassId));

        //var obj = ((TreeViewItem) e.NewValue).Tag;
        //var symbol = obj as DeclarationSymbol;
        //var id = symbol != null ? symbol.Id : (obj == null ? 0 : obj.GetHashCode());
        //try
        //{
        //  _propertyGrid.SelectedObject = obj;
        //}
        //catch
        //{
        //}
        //_objectType.Text = obj == null ? "<null>" : obj.GetType().FullName + " [" + id + "]";
      }
      else
        _interactiveSpanInfos = ImmutableArray<SpanInfo>.Empty;

      _textEditor.TextArea.TextView.Redraw();
    }
  }
}
