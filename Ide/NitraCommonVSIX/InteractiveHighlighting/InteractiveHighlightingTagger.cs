using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

using Nitra.ClientServer.Messages;
using Nitra.VisualStudio.Models;

using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Collections.Immutable;
using static Nitra.ClientServer.Messages.AsyncServerMessage;

using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;

namespace Nitra.VisualStudio.BraceMatching
{
  public class InteractiveHighlightingTagger : ITagger<TextMarkerTag>
  {
    readonly ITextView      _textView;
    readonly ITextBuffer    _textBuffer;
             SnapshotPoint? _caretPosOpt;

    //Событие у второго эземпляра не подключаются. Так же жопа с маос_ховер.
    public event EventHandler<SnapshotSpanEventArgs>  TagsChanged;

    public InteractiveHighlightingTagger(ITextView textView, ITextBuffer textBuffer)
    {
      _textView    = textView;
      _textBuffer  = textBuffer;
      _caretPosOpt = null;

      _textView.Caret.PositionChanged         += CaretPositionChanged;
      _textView.LayoutChanged                 += ViewLayoutChanged;
      _textView.Closed                        += _textView_Closed;
      ((UIElement)_textView).IsVisibleChanged += Elem_IsVisibleChanged;

      UpdateAtCaretPosition(_textView.Caret.Position);
    }

    private void _textView_Closed(object sender, EventArgs e)
    {
      _textView.Caret.PositionChanged         -= CaretPositionChanged;
      _textView.LayoutChanged                 -= ViewLayoutChanged;
      _textView.Closed                        -= _textView_Closed;
      ((UIElement)_textView).IsVisibleChanged -= Elem_IsVisibleChanged;
    }

    private void Elem_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
      var isVisible = (bool)e.NewValue;
      var wpfTextView = (IWpfTextView)_textView;

      if (isVisible)
      {
        var textViewModelOpt = GetTextViewModelOpt();
        if (textViewModelOpt == null)
        {
          // It happens when second view was opened. For example if user split a editor.
          var fileModel = _textBuffer.Properties.GetProperty<FileModel>(Constants.FileModelKey);
          textViewModelOpt = VsUtils.GetOrCreateTextViewModel(wpfTextView, fileModel);
        }
      }
      else
      {
        var textViewModelOpt = GetTextViewModelOpt();
        if (textViewModelOpt != null)
        {
          var fileModel = textViewModelOpt.FileModel;
          fileModel.Remove(wpfTextView);
        }
      }
    }

    public ITextView TextView => _textView;

    // don't cache it! Property can be changed in _textView.Properties when the view hide or show.
    TextViewModel GetTextViewModelOpt()
    {
      if (_textView.Properties.TryGetProperty<TextViewModel>(Constants.TextViewModelKey, out var value))
        return value;

      return null;
    }

    void ViewLayoutChanged(object source, TextViewLayoutChangedEventArgs e)
    {
      if (e.NewSnapshot != e.OldSnapshot) //make sure that there has really been a change
        UpdateAtCaretPosition(_textView.Caret.Position);
    }

    void CaretPositionChanged(object _, CaretPositionChangedEventArgs e)
    {
      UpdateAtCaretPosition(e.NewPosition);
    }

    void UpdateAtCaretPosition(CaretPosition caretPosition)
    {
      var textViewModel = GetTextViewModelOpt();
      if (textViewModel == null)
        return;

      _caretPosOpt = caretPosition.Point.GetPoint(_textBuffer, caretPosition.Affinity);

      if (_caretPosOpt.HasValue)
      {
        var fileModel = textViewModel.FileModel;
        var pos = _caretPosOpt.Value;
        fileModel.CaretPositionChanged(pos.Position, pos.Snapshot.Version.Convert());
      }
      else
        textViewModel.Reset();
    }

    public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      var textViewModel = GetTextViewModelOpt();

      if (textViewModel == null)
        yield break;

      var currentSnapshot = _textBuffer.CurrentSnapshot;

      if (_caretPosOpt.HasValue)
      {
        var matchedBrackets = textViewModel.MatchedBrackets;
        var caretPos        = _caretPosOpt.Value;
        var lastSnapshot    = caretPos.Snapshot;

        if (matchedBrackets != null && lastSnapshot.Version.VersionNumber == matchedBrackets.Version + 1)
        {
          var tagName = "blue";
          foreach (MatchBrackets pair in matchedBrackets.results)
          {
            yield return MakeTagSpan(lastSnapshot, currentSnapshot, pair.Open, tagName);
            yield return MakeTagSpan(lastSnapshot, currentSnapshot, pair.Close, tagName);
            tagName = Constants.BraceMatchingSecond;
          }
        }
      }

      var findSymbolReferences = textViewModel.FindSymbolReferences;
      var fileId               = textViewModel.FileModel.Id;
      var fileVersion          = new FileVersion(currentSnapshot.Version.VersionNumber - 1);

      if (findSymbolReferences != null)
      {
        foreach (var symbolRefs in findSymbolReferences.symbols)
        {
          foreach (var definition in symbolRefs.Definitions)
          {
            var loc = definition.Location;
            var file = loc.File;
            if (file.FileId != fileId || file.FileVersion != fileVersion)
              continue;
            yield return MakeTagSpan(currentSnapshot, loc.Span, Constants.DefenitionHighlighting);
          }

          foreach (var fileEntries in symbolRefs.References)
          {
            var file = fileEntries.File;
            if (file.FileId != fileId || file.FileVersion != fileVersion)
              continue;
            foreach (var range in fileEntries.Ranges)
              yield return MakeTagSpan(currentSnapshot, range.Span, Constants.ReferenceHighlighting);
          }
        }
      }
    }

    public static TagSpan<TextMarkerTag> MakeTagSpan(ITextSnapshot currentSnapshot, NSpan nSpan, string tagType)
    {
      var span = new SnapshotSpan(currentSnapshot, VsUtils.Convert(nSpan));
      return new TagSpan<TextMarkerTag>(span, new TextMarkerTag(tagType));
    }

    public static TagSpan<TextMarkerTag> MakeTagSpan(ITextSnapshot lastSnapshot, ITextSnapshot currentSnapshot, NSpan nSpan, string tagType)
    {
      var span           = new SnapshotSpan(lastSnapshot, VsUtils.Convert(nSpan));
      var translatedSpan = span.TranslateTo(currentSnapshot, SpanTrackingMode.EdgeExclusive);
      return new TagSpan<TextMarkerTag>(translatedSpan, new TextMarkerTag(tagType));
    }

    internal void Update()
    {
      TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, _textBuffer.CurrentSnapshot.Length)));
    }
  }
}
