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
    [Import]
    private ITextDocumentFactoryService _textDocumentFactoryService = null;

    [Import]
    private ITextStructureNavigatorSelectorService _navigatorService = null;

    public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
    {
      return new NitraCompletionSource(textBuffer, _textDocumentFactoryService, _navigatorService, XXLanguageXXVsPackagePackage.Language);
    }
  }
}
