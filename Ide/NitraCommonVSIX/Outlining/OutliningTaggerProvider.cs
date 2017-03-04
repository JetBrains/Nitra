using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nitra.VisualStudio
{
  [Export(typeof(ITaggerProvider))]
  [TagType(typeof(IOutliningRegionTag))]
  [ContentType("code")]
  public class OutliningTaggerProvider : ITaggerProvider
  {
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
      //create a single tagger for each buffer.
      Func<ITagger<T>> sc = delegate() { return (ITagger<T>)new OutliningTagger(buffer); };
      return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(Constants.OutliningTaggerKey, sc);    }
  }
}
