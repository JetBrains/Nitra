﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using System.Timers;
using System.Diagnostics;
﻿using System.Windows.Controls;
﻿using Nitra;
﻿using Nitra.VisualStudio;
﻿using Nitra.VisualStudio.Parsing;

namespace XXNamespaceXX
{
  /// <summary>
  /// Shows errors in the error list
  /// </summary>
  internal class ErrorListPresenter : IErrorsReporter
  {
    private readonly IWpfTextView                     _textView;
    private readonly SimpleTagger<ErrorTag>           _squiggleTagger;
    private readonly ErrorListProvider                _errorListProvider;
    private readonly List<TrackingTagSpan<IErrorTag>> _previousSquiggles;
    private readonly List<ErrorTask>                  _previousErrors;

    public ErrorListPresenter(IWpfTextView textView, IErrorProviderFactory squiggleProviderFactory, IServiceProvider serviceProvider)
    {
      _textView = textView;
      textView.TextBuffer.Changed += OnTextBufferChanged;
      textView.Closed             += OnTextViewClosed;

      _squiggleTagger    = squiggleProviderFactory.GetErrorTagger(textView.TextBuffer);
      _errorListProvider         = new Microsoft.VisualStudio.Shell.ErrorListProvider(serviceProvider);
      _previousErrors    = new List<ErrorTask>();
      _previousSquiggles = new List<TrackingTagSpan<IErrorTag>>();
    }

    void OnTextViewClosed(object sender, EventArgs e)
    {
      // when a text view is closed we want to remove the corresponding errors from the error list
      ClearErrors();
      _errorListProvider.Dispose();
    }

    void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
    {
      // keep the list of errors updated every time the buffer changes
    }

    private void ClearErrors()
    {
      _previousSquiggles.ForEach(tag => _squiggleTagger.RemoveTagSpans(t => tag.Span == t.Span));
      _previousSquiggles.Clear();
      _previousErrors.ForEach(task => _errorListProvider.Tasks.Remove(task));
      _previousErrors.Clear();
    }

    /// <summary>
    /// Called when the user double-clicks on an entry in the Error List
    /// </summary>
    private void OnTaskNavigate(object source, EventArgs e)
    {
      ErrorTask task = source as ErrorTask;
      if (task != null)
      {
        // move the caret to position of the error
        _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, _textView.TextSnapshot.GetLineFromLineNumber(task.Line).Start + task.Column));
        // set focus to make sure the error is visible to the user
        _textView.VisualElement.Focus();
      }
    }

    private TaskErrorCategory TranslateErrorCategory(Error error)
    {
      return TaskErrorCategory.Error;
      //switch (error)
      //{
      //  case ValidationErrorSeverity.Error:
      //    return TaskErrorCategory.Error;
      //  case ValidationErrorSeverity.Message:
      //    return TaskErrorCategory.Message;
      //  case ValidationErrorSeverity.Warning:
      //    return TaskErrorCategory.Warning;
      //}
      //
      //return TaskErrorCategory.Error;
    }

    public void ReportParseErrors(ParseResult parseResult, ITextSnapshot snapshot)
    {
      _errorListProvider.SuspendRefresh();
      try
      {
        // remove any previously created errors to get a clean start
        ClearErrors();

        foreach (var error in parseResult.GetErrors())
        {
          // creates the instance that will be added to the Error List
          var nSpan           = error.Location.Span;
          var span            = new Span(nSpan.StartPos, nSpan.EndPos);
          ErrorTask task      = new ErrorTask();
          task.Category       = TaskCategory.All;
          task.Priority       = TaskPriority.Normal;
          task.Document       = _textView.TextBuffer.Properties.GetProperty<ITextDocument>(typeof (ITextDocument)).FilePath;
          task.ErrorCategory  = TranslateErrorCategory(error);
          task.Text           = error.Message;
          task.Line           = _textView.TextSnapshot.GetLineNumberFromPosition(span.Start);
          task.Column         = span.Start - _textView.TextSnapshot.GetLineFromLineNumber(task.Line).Start;
          task.Navigate       += OnTaskNavigate;
          _errorListProvider.Tasks.Add(task);
          _previousErrors.Add(task);

          var trackingSpan = _textView.TextSnapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeNegative);
          _squiggleTagger.CreateTagSpan(trackingSpan, new ErrorTag("syntax error", error.Message));
          _previousSquiggles.Add(new TrackingTagSpan<IErrorTag>(trackingSpan, new ErrorTag("syntax error", error.Message)));
        }
      }
      finally { _errorListProvider.ResumeRefresh(); }    }

    public void ReportParseException(ParseFailedEventArgs arg)
    {
      var pfe = arg.Exception as ParsingFailureException;

      if (pfe != null)
        ReportParseErrors(pfe.ParseResult, arg.Snapshot);
      else
      {
        var error = new ErrorTask();
        error.ErrorCategory = TaskErrorCategory.Error;
        error.Category = TaskCategory.All;
        error.Text = "INE: " + arg.Exception.Message + Environment.NewLine + @"Please contact developers.";
        error.ErrorCategory = TaskErrorCategory.Error;
        error.Document = arg.FileName;
        _errorListProvider.Tasks.Add(error);
      }
    }
  }
}
