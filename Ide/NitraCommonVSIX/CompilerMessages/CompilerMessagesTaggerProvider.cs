using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;

namespace Nitra.VisualStudio.CompilerMessages
{
  [Export(typeof(ITaggerProvider))]
  [ContentType("nitra")]
  [TagType(typeof(ErrorTag))]
  class CompilerMessagesTaggerProvider : ITaggerProvider
  {
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
      return (ITagger<T>)buffer.Properties.GetOrCreateSingletonProperty(Constants.CompilerMessagesTaggerKey, 
        () => new CompilerMessagesTagger(buffer));
    }
  }
}
