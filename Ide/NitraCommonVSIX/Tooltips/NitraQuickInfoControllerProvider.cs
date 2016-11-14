using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace EPiServer.Labs.LangFilesExtension.Core.QuickInfo
{
  [Export(typeof(IIntellisenseControllerProvider))]
  [Name("Nitra ToolTip QuickInfo Controller")]
  [ContentType("nitra")]
  internal class NitraQuickInfoControllerProvider : IIntellisenseControllerProvider
  {
    [Import] internal IQuickInfoBroker QuickInfoBroker { get; set; }

    public IIntellisenseController TryCreateIntellisenseController(ITextView textView, IList<ITextBuffer> subjectBuffers)
    {
      return new NitraQuickInfoController(textView, subjectBuffers, this);
    }
  }
}
