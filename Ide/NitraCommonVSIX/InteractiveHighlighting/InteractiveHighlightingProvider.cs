using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Nitra.VisualStudio.BraceMatching
{
  [Export(typeof(IViewTaggerProvider) )]
  [ContentType("nitra")]
  [TagType(typeof(TextMarkerTag))]
  internal sealed class InteractiveHighlightingProvider : IViewTaggerProvider
  {
    [Import] ITextDocumentFactoryService _textDocumentFactoryService = null;

    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
      return (ITagger<T>)buffer.Properties.GetOrCreateSingletonProperty(Constants.BraceMatchingTaggerKey, 
        () => new InteractiveHighlightingTagger(textView, buffer));
    }
  }
}
