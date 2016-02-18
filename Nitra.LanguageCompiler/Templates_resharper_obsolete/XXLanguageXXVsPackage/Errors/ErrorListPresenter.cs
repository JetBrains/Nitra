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
﻿using Microsoft.VisualStudio;
﻿using Microsoft.VisualStudio.Shell.Interop;
﻿using Microsoft.VisualStudio.TextManager.Interop;
﻿using Nitra;
using Nitra.ProjectSystem;
﻿using Nitra.VisualStudio;

namespace XXNamespaceXX
{
  /// <summary>
  /// Shows errors in the error list
  /// </summary>
  internal class ErrorListPresenter : IErrorsReporter
  {
    private readonly ITextBuffer _textBuffer;
    private readonly SimpleTagger<ErrorTag> _squiggleTagger;
    private readonly ErrorListProvider _errorListProvider;
    private readonly List<TrackingTagSpan<IErrorTag>> _previousSquiggles;
    private readonly List<ErrorTask> _previousErrors;
    private readonly IServiceProvider _serviceProvider;

    public ErrorListPresenter(ITextBuffer textBuffer, IErrorProviderFactory squiggleProviderFactory, IServiceProvider serviceProvider)
    {
      _textBuffer = textBuffer;
      _textBuffer.Changed += OnTextBufferChanged;

      _serviceProvider = serviceProvider;
      _squiggleTagger = squiggleProviderFactory.GetErrorTagger(_textBuffer);
      _errorListProvider = new Microsoft.VisualStudio.Shell.ErrorListProvider(serviceProvider);
      _previousErrors = new List<ErrorTask>();
      _previousSquiggles = new List<TrackingTagSpan<IErrorTag>>();
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
        OpenDocumentAndNavigateTo(task.Document, task.Line, task.Column);
      }
    }

    private void OpenDocumentAndNavigateTo(string path, int line, int column)
    {
      var openDoc = (IVsUIShellOpenDocument)_serviceProvider.GetService(typeof(IVsUIShellOpenDocument));

      if (openDoc == null)
        return;

      IVsWindowFrame frame; // IVsWindowFrame
      Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp; // Microsoft.VisualStudio.OLE.Interop.IServiceProvider
      IVsUIHierarchy hier; // IVsUIHierarchy
      uint itemid; // uint
      var logicalView = VSConstants.LOGVIEWID_Code; // Guid

      if (ErrorHandler.Failed(openDoc.OpenDocumentViaProject(path, ref logicalView, out sp, out hier, out itemid, out frame)) || frame == null)
        return;

      object docData;
      frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out docData);

      // Get the VsTextBuffer
      VsTextBuffer buffer = docData as VsTextBuffer;
      if (buffer == null)
      {
        IVsTextLines lines; // IVsTextLines
        var bufferProvider = docData as IVsTextBufferProvider;
        if (bufferProvider != null)
          ErrorHandler.ThrowOnFailure(bufferProvider.GetTextBuffer(out lines));
      }

      if (buffer == null)
        return;

      // Finally, perform the navigation.
      var mgr = (IVsTextManager)_serviceProvider.GetService(typeof(VsTextManagerClass));
      if (mgr == null)
        return;

      mgr.NavigateToLineAndColumn(buffer, ref logicalView, line, column, line, column);
    }

    private static TaskErrorCategory TranslateErrorCategory(CompilerMessage error)
    {
      switch (error.Type)
      {
        case CompilerMessageType.FatalError:
        case CompilerMessageType.Error:
          return TaskErrorCategory.Error;
        case CompilerMessageType.Warning:
          return TaskErrorCategory.Warning;
        case CompilerMessageType.Hint:
          return TaskErrorCategory.Message;
      }

      return TaskErrorCategory.Error;
    }

    public void ReportParseErrors(IParseResult parseResult, ITextSnapshot snapshot)
    {
      _errorListProvider.SuspendRefresh();
      try
      {
        // remove any previously created errors to get a clean start
        ClearErrors();

        var messages = (CompilerMessageList)parseResult.CompilerMessages;

        foreach (var error in messages.GetMessages())
        {
          // creates the instance that will be added to the Error List
          var nSpan = error.Location.Span;
          var span = new Span(nSpan.StartPos, nSpan.Length);
          if (span.Start >= snapshot.Length)
            continue;
          ErrorTask task = new ErrorTask();
          task.Category = TaskCategory.All;
          task.Priority = TaskPriority.Normal;
          task.Document = _textBuffer.Properties.GetProperty<ITextDocument>(typeof(ITextDocument)).FilePath;
          task.ErrorCategory = TranslateErrorCategory(error);
          task.Text = error.Text;
          task.Line = snapshot.GetLineNumberFromPosition(span.Start);
          task.Column = span.Start - snapshot.GetLineFromLineNumber(task.Line).Start;
          task.Navigate += OnTaskNavigate;
          _errorListProvider.Tasks.Add(task);
          _previousErrors.Add(task);

          var trackingSpan = snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeNegative);
          _squiggleTagger.CreateTagSpan(trackingSpan, new ErrorTag("syntax error", error.Text));
          _previousSquiggles.Add(new TrackingTagSpan<IErrorTag>(trackingSpan, new ErrorTag("syntax error", error.Text)));
        }
      }
      finally { _errorListProvider.ResumeRefresh(); }
    }

    public void ReportParseException(Exception exception, string fileName, ITextSnapshot snapshot)
    {
      var pfe = exception as ParsingFailureException;

      if (pfe != null)
        ReportParseErrors(pfe.ParseResult, snapshot);
      else
      {
        var error = new ErrorTask();
        error.ErrorCategory = TaskErrorCategory.Error;
        error.Category      = TaskCategory.All;
        error.Text          = "INE: " + exception.Message + Environment.NewLine + @"Please contact developers.";
        error.ErrorCategory = TaskErrorCategory.Error;
        error.Document      = fileName;
        _errorListProvider.Tasks.Add(error);
      }
    }

    public void Dispose()
    {
      ClearErrors();
      _errorListProvider.Dispose();
    }
  }
}
