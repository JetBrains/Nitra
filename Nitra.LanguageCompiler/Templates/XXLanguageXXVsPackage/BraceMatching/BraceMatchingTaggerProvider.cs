using Nitra.VisualStudio;
using Nitra.VisualStudio.Parsing;

using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace XXNamespaceXX
{
  [Export(typeof(IViewTaggerProvider) )]
  [ContentType("XXLanguageXX")]
  [TagType(typeof(TextMarkerTag))]
  internal sealed class BraceMatchingTaggerProvider : IViewTaggerProvider
  {
    [Import]
    private ITextDocumentFactoryService _textDocumentFactoryService = null;

    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
      BraceMatchingTagger braceMatchingTagger;
      if (buffer.Properties.TryGetProperty(TextBufferProperties.BraceMatchingTagger, out braceMatchingTagger))
        return (ITagger<T>)braceMatchingTagger;

      var parseAgent = Utils.TryGetOrCreateParseAgent(buffer, _textDocumentFactoryService, NitraPackage.Instance.Language);
      var tagger = new BraceMatchingTagger(Constants.ErrorClassificationTypeName, parseAgent, textView, buffer);
      buffer.Properties.AddProperty(TextBufferProperties.BraceMatchingTagger, tagger);
      return (ITagger<T>)tagger;
    }
  }
}
