using Nitra.VisualStudio.Coloring;
using Nitra.VisualStudio.Parsing;

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
  [ContentType("XXLanguageXX")]
  internal sealed class XXLanguageXXClassifierProvider : IClassifierProvider
  {
    /// The ClassificationTypeRegistryService is used to discover the types defined in ClassificationTypeDefinitions
    [Import]
    private IClassificationTypeRegistryService ClassificationTypeRegistry { get; set; }

    [Import]
    private ITextDocumentFactoryService _textDocumentFactoryService = null;

    public IClassifier GetClassifier(ITextBuffer buffer)
    {
      NitraClassifier classifier;

      if (buffer.Properties.TryGetProperty(TextBufferProperties.NitraClassifier, out classifier))
        return classifier;

      var parseAgent = NitraVsUtils.TryGetOrCreateParseAgent(buffer, _textDocumentFactoryService, NitraPackage.Instance.Language);
      classifier = new NitraClassifier(parseAgent, buffer, ClassificationTypeRegistry);
      buffer.Properties.AddProperty(TextBufferProperties.NitraClassifier, classifier);
      return classifier;
    }
  }
}
