using Microsoft.VisualStudio.Data.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

using Nitra.VisualStudio;

using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace XXNamespaceXX
{
  [Export(typeof(IViewTaggerProvider) )]
  [ContentType("XXLanguageXX")]
  [TagType(typeof(TextMarkerTag))]
  internal sealed class BraceMatchingTaggerProvider : IViewTaggerProvider
  {
    [Import] ITextDocumentFactoryService _textDocumentFactoryService = null;

    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
      BraceMatchingTagger braceMatchingTagger;
      if (buffer.Properties.TryGetProperty(TextBufferProperties.BraceMatchingTagger, out braceMatchingTagger))
        return (ITagger<T>)braceMatchingTagger;

//      var nitraSolutionService = XXNamespaceXX.ReSharperSolution.XXLanguageXXSolution;
      var tagger = new BraceMatchingTagger(Constants.ErrorClassificationTypeName, textView, buffer, null);
      buffer.Properties.AddProperty(TextBufferProperties.BraceMatchingTagger, tagger);
      return (ITagger<T>)tagger;
    }
  }
}
