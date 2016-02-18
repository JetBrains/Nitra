using Nitra.VisualStudio;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

namespace XXNamespaceXX
{
  /// <summary>
  /// Adds the error list support when the view is created
  /// </summary>
  [Export(typeof(IWpfTextViewCreationListener))]
  [ContentType("XXLanguageXX")]
  [TextViewRole(PredefinedTextViewRoles.Document)]
  internal class ErrorListPresenterFactory : IWpfTextViewCreationListener
  {
    [Import]                             IErrorProviderFactory _errorProviderFactory = null;
    [Import(typeof(SVsServiceProvider))] IServiceProvider      _serviceProviderServiceProvider = null;

    public void TextViewCreated(IWpfTextView textView)
    {
      // Add the error list support to the just created view
      textView.TextBuffer.Properties.GetOrCreateSingletonProperty<ErrorListPresenter>(() =>
          new ErrorListPresenter(textView.TextBuffer, _errorProviderFactory, _serviceProviderServiceProvider)
      );
    }
  }
}
