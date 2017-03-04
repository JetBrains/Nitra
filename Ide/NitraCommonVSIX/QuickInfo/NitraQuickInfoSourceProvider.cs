using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

using System.ComponentModel.Composition;

namespace Nitra.VisualStudio.QuickInfo
{
  [Export(typeof(IQuickInfoSourceProvider))]
  [Name("NitraQuickInfo")]
  [Order(Before = "squiggle")]
  [ContentType("nitra")]
  internal sealed class NitraQuickInfoSourceProvider : IQuickInfoSourceProvider
  {
    [Import]
    internal ITextStructureNavigatorSelectorService NavigatorService { get; private set; }

    public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
    {
      return textBuffer.Properties.GetOrCreateSingletonProperty(Constants.NitraQuickInfoSourceKey, () => new NitraQuickInfoSource(textBuffer, NavigatorService));
    }
  }
}
