using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Language.Intellisense;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text.Operations;

namespace Nitra.VisualStudio.CodeCompletion
{
  [Export(typeof(ICompletionSourceProvider))]
  [ContentType("nitra")]
  [Name("Nitra word completion")]
  class NitraCompletionSourceProvider : ICompletionSourceProvider
  {
    [Import]
    internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

    public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
    {
      return new NitraCompletionSource(this, textBuffer);
    }
  }
}
