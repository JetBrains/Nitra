using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using Nitra.ClientServer.Messages;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nitra.VisualStudio.CodeCompletion
{
  class NitraCompletionSource : ICompletionSource
  {
    readonly ITextBuffer                   _textBuffer;
             bool                          _isDisposed;
             NitraCompletionSourceProvider _sourceProvider;

    public NitraCompletionSource(NitraCompletionSourceProvider sourceProvider, ITextBuffer textBuffer)
    {
      _sourceProvider = sourceProvider;
      _textBuffer     = textBuffer;
    }

    void ICompletionSource.AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
    {
      var fileModel = VsUtils.TryGetFileModel(_textBuffer);

      if (fileModel == null)
        return;

      var client  = fileModel.Server.Client;
      var triggerPoint = session.GetTriggerPoint(_textBuffer);
      var snapshot = _textBuffer.CurrentSnapshot;
      var version = snapshot.Version.Convert();

      client.Send(new ClientMessage.CompleteWord(fileModel.Id, version, triggerPoint.GetPoint(snapshot).Position));
      var result = client.Receive<ServerMessage.CompleteWord>();
      var span = result.replacementSpan;
      var applicableTo = snapshot.CreateTrackingSpan(new Span(span.StartPos, span.Length), SpanTrackingMode.EdgeInclusive);

      var completions = new List<Completion>();

      CompletionElem.Literal literal;
      CompletionElem.Symbol  symbol;

      foreach (var elem in result.completionList)
      {
        if ((literal = elem as CompletionElem.Literal) != null)
          completions.Add(new Completion(literal.text, literal.text, "literal", null, null));
        else if ((symbol = elem as CompletionElem.Symbol) != null)
          completions.Add(new Completion(symbol.name, symbol.name, symbol.description, null, null));
      }

      completionSets.Add(new CompletionSet("NitraWordCompletion", "Nitra word completion", applicableTo, completions, null));
    }

    public void Dispose()
    {
      if (!_isDisposed)
      {
        GC.SuppressFinalize(this);
        _isDisposed = true;
      }
    }
  }
}
