using Nitra.VisualStudio;
using Microsoft.VisualStudio.Data.Core;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

using Nitra.VisualStudio.Coloring;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;

namespace XXNamespaceXX
{
  [Export(typeof(IClassifierProvider))]
  [ContentType("XXLanguageXX")]
  internal sealed class XXLanguageXXClassifierProvider : IClassifierProvider
  {
    /// The ClassificationTypeRegistryService is used to discover the types defined in ClassificationTypeDefinitions
    [Import] IClassificationTypeRegistryService  _classificationTypeRegistry = null;
    [Import] ITextDocumentFactoryService         _textDocumentFactoryService = null;

    public IClassifier GetClassifier(ITextBuffer buffer)
    {
      NitraClassifier classifier;

      if (buffer.Properties.TryGetProperty(TextBufferProperties.NitraClassifier, out classifier))
        return classifier;

      //var nitraSolutionService = XXNamespaceXX.ReSharperSolution.XXLanguageXXSolution;
      classifier = new NitraClassifier(buffer, _classificationTypeRegistry, null);
      buffer.Properties.AddProperty(TextBufferProperties.NitraClassifier, classifier);
      return classifier;
    }
  }
}
