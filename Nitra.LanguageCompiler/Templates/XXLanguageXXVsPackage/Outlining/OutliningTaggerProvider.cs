using Nitra.VisualStudio;
using Nitra.VisualStudio.Outlining;
using Nitra.VisualStudio.Parsing;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;


namespace XXNamespaceXX
{
  [Export(typeof(ITaggerProvider))]
  [ContentType("XXLanguageXX")]
  [TagType(typeof(IOutliningRegionTag))]
  internal sealed class OutliningTaggerProvider : ITaggerProvider
  {

    [Import]
    private ITextDocumentFactoryService _textDocumentFactoryService = null;

    public ITagger<T> CreateTagger<T>(ITextBuffer buffer)
      where T : ITag
    {
      OutliningTagger tagger;
      if (buffer.Properties.TryGetProperty(TextBufferProperties.OutliningTagger, out tagger))
        return (ITagger<T>)tagger;

      var parseAgent = Utils.TryGetOrCreateParseAgent(buffer, _textDocumentFactoryService, NitraPackage.Instance.Language);
      tagger = new OutliningTagger(parseAgent, buffer);
      buffer.Properties.AddProperty(TextBufferProperties.OutliningTagger, tagger);
      return (ITagger<T>)tagger;
    }
  }
}
