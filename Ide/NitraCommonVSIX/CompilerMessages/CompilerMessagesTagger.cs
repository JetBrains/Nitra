using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;

using Project = EnvDTE.Project;
using Nitra.ClientServer.Messages;

namespace Nitra.VisualStudio.CompilerMessages
{
  public class CompilerMessagesTagger : ITagger<ErrorTag>
  {
    private readonly ITextBuffer _textBuffer;
    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    public CompilerMessagesTagger(ITextBuffer buffer)
    {
      _textBuffer = buffer;
    }

    public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      var fileModel = VsUtils.TryGetFileModel(_textBuffer);

      if (fileModel == null)
        yield break;

      var currentSnapshot = _textBuffer.CurrentSnapshot;
      var msgKind         = fileModel.CompilerMessages;
      var snapshots       = fileModel.CompilerMessagesSnapshots;

      for (int i = 0; i < msgKind.Length; i++)
      {
        var msgs     = msgKind[i];
        var snapshot = snapshots[i];

        foreach (var span in spans)
        {
          foreach (var msg in msgs)
          {
            var snapshotSpan = ToSnapshotSpan(currentSnapshot, msg, snapshot);
            if (span.IntersectsWith(snapshotSpan))
              yield return TagSpanFromMessage(msg, snapshotSpan);
          }
        }
      }
    }

    static SnapshotSpan ToSnapshotSpan(ITextSnapshot currentSnapshot, CompilerMessage msg, ITextSnapshot snapshot)
    {
      var nSpan = msg.Location.Span;
      var span = new Span(nSpan.StartPos, nSpan.Length);
      return new SnapshotSpan(snapshot, span).TranslateTo(currentSnapshot, SpanTrackingMode.EdgeExclusive);
    }

    static TagSpan<ErrorTag> TagSpanFromMessage(CompilerMessage msg, SnapshotSpan snapshotSpan)
    {
      var errorTag = new ErrorTag(ConvertMessageType(msg.Type), msg.Text);
      return new TagSpan<ErrorTag>(snapshotSpan, errorTag);
    }

    static string ConvertMessageType(CompilerMessageType type)
    {
      switch (type)
      {
        case CompilerMessageType.FatalError:
          return PredefinedErrorTypeNames.OtherError;
        case CompilerMessageType.Error:
          return PredefinedErrorTypeNames.SyntaxError;
        case CompilerMessageType.Warning:
          return PredefinedErrorTypeNames.Warning;
        case CompilerMessageType.Hint:
          return PredefinedErrorTypeNames.Suggestion;
        default:
          return PredefinedErrorTypeNames.OtherError;
      }
    }

    internal void Update()
    {
      var snapshot = _textBuffer.CurrentSnapshot;
      var span     = new SnapshotSpan(snapshot, new Span(0, snapshot.Length));
      TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
    }
  }
}