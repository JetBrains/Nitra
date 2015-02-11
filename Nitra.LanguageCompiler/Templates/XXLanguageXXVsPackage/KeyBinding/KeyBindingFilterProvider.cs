using Nitra.VisualStudio;
using Nitra.VisualStudio.KeyBinding;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

using VSConstants = Microsoft.VisualStudio.VSConstants;

namespace XXNamespaceXX
{
  [Export(typeof(IVsTextViewCreationListener))]
  [ContentType("XXLanguageXX")]
  [TextViewRole(PredefinedTextViewRoles.Editable)]
  internal class KeyBindingCommandFilterProvider : IVsTextViewCreationListener
  {
    [Import]
    private ITextDocumentFactoryService _textDocumentFactoryService = null;

    public void VsTextViewCreated(IVsTextView textViewAdapter)
    {
      new KeyBindingCommandFilter(textViewAdapter, _textDocumentFactoryService);
    }
  }
}
