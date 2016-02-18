using Microsoft.VisualStudio.Data.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

using Nitra.VisualStudio;
using Nitra.VisualStudio.Outlining;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace XXNamespaceXX
{
  [Export(typeof(ITaggerProvider))]
  [ContentType("XXLanguageXX")]
  [TagType(typeof(IOutliningRegionTag))]
  internal sealed class OutliningTaggerProvider : ITaggerProvider
  {
    [Import] ITextDocumentFactoryService _textDocumentFactoryService = null;

    public ITagger<T> CreateTagger<T>(ITextBuffer buffer)
      where T : ITag
    {
      OutliningTagger tagger;
      if (buffer.Properties.TryGetProperty(TextBufferProperties.OutliningTagger, out tagger))
        return (ITagger<T>)tagger;

      //var nitraSolutionService = XXNamespaceXX.ReSharperSolution.XXLanguageXXSolution;
      tagger = new OutliningTagger(buffer, null);
      buffer.Properties.AddProperty(TextBufferProperties.OutliningTagger, tagger);
      return (ITagger<T>)tagger;
    }
  }
}
