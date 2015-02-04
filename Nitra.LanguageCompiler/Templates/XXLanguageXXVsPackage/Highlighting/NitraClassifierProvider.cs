using Nitra.VisualStudio.Coloring;
using Nitra.VisualStudio.Parsing;

using Nemerle;
using Nemerle.Collections;
using Nemerle.Utility;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using Nitra.VisualStudio;

namespace XXNamespaceXX
{
  [Export(typeof(IClassifierProvider))]
  [ContentType("text")]
  internal sealed class XXLanguageXXClassifierProvider : IClassifierProvider
  {
    /// The ClassificationTypeRegistryService is used to discover the types defined in ClassificationTypeDefinitions
    [Import]
    private IClassificationTypeRegistryService ClassificationTypeRegistry { get; set; }

    public IClassifier GetClassifier(ITextBuffer buffer)
    {
      ParseAgent      parseAgent;
      NitraClassifier classifier;

      if (buffer.Properties.TryGetProperty(TextBufferProperties.NitraClassifier, out classifier))
        return classifier;
      
      if (Utils.TryGetOrCreateParseAgent(buffer, out parseAgent))
      {
        classifier = new NitraClassifier(parseAgent, buffer, ClassificationTypeRegistry);
        buffer.Properties.AddProperty(TextBufferProperties.NitraClassifier, classifier);
        return classifier;
      }

      return null;
    }
  }
}
