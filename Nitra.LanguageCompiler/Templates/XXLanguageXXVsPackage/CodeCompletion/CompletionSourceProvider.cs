using Microsoft.VisualStudio.Data.Core;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

using Nitra.VisualStudio;

using System.ComponentModel.Composition;

namespace XXNamespaceXX
{
  [Export(typeof(ICompletionSourceProvider))]
  [ContentType("XXLanguageXX")]
  [Name("XXLanguageXX token completion")]
  class XXLanguageXXTokenCompletionSourceProvider : ICompletionSourceProvider
  {
    [Import] ITextDocumentFactoryService            _textDocumentFactoryService = null;
    [Import] ITextStructureNavigatorSelectorService _navigatorService           = null;

    public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
    {
      //var nitraSolutionService = XXNamespaceXX.ReSharperSolution.XXLanguageXXSolution;
      return new NitraCompletionSource(textBuffer, _textDocumentFactoryService, _navigatorService, null);
    }
  }
}
