using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

using System.ComponentModel.Composition;

namespace Nitra.VisualStudio.Definitions
{
  static class NitraTypeDefinitions
  {
    [Export]
    [Name("nitra")]
    [BaseDefinition("code")]
    public static ContentTypeDefinition NitraContentTypeDefinition = null;
  }
}
