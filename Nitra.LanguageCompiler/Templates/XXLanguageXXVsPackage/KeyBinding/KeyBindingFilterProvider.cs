using Nitra.VisualStudio.KeyBinding;

using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

using System.ComponentModel.Composition;

using IServiceProvider = System.IServiceProvider;

namespace Nitra.CSharp
{
  [Export(typeof(IVsTextViewCreationListener))]
  [ContentType("XXLanguageXX")]
  [TextViewRole(PredefinedTextViewRoles.Editable)]
  internal class KeyBindingCommandFilterProvider : IVsTextViewCreationListener
  {
    [Import]
    ITextDocumentFactoryService _textDocumentFactoryService = null;
    [Import(typeof(SVsServiceProvider))]
    IServiceProvider _serviceProvider = null;
    [Import]
    ICompletionBroker _completionBroker = null;
    [Import]
    IVsEditorAdaptersFactoryService _adaptersFactory = null;

    public void VsTextViewCreated(IVsTextView textViewAdapter)
    {
      new KeyBindingCommandFilter(textViewAdapter, _textDocumentFactoryService, _serviceProvider, _completionBroker, _adaptersFactory);
    }
  }
}
