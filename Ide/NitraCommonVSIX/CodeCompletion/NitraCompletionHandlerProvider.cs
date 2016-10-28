using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;

namespace Nitra.VisualStudio.CodeCompletion
{
  [Export(typeof(IVsTextViewCreationListener))]
  [Name("Nitra word completion handler")]
  [ContentType("nitra")]
  [TextViewRole(PredefinedTextViewRoles.Editable)]
  class NitraCompletionHandlerProvider : IVsTextViewCreationListener
  {
    [Import] private  IVsEditorAdaptersFactoryService _adapterService = null;
    [Import] internal ICompletionBroker               CompletionBroker { get; private set; }
    [Import] internal SVsServiceProvider              ServiceProvider { get; private set; }

    public void VsTextViewCreated(IVsTextView textViewAdapter)
    {
      IWpfTextView textView = _adapterService.GetWpfTextView(textViewAdapter);
      if (textView == null)
        return;

      Func<NitraCompletionCommandHandler> createCommandHandler = 
        delegate () { return new NitraCompletionCommandHandler(textViewAdapter, textView, this); };
      textView.Properties.GetOrCreateSingletonProperty(createCommandHandler);
    }
  }
}
