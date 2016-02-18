using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

using System.ComponentModel.Composition;
using System.Windows.Media;

namespace XXNamespaceXX
{
  internal static partial class NitraFileExtensionsAndContentTypeDefinition
  {
    [Export]
    [FileExtension("XXFileExtensionXX")]
    [ContentType("XXLanguageXX")]
    internal static FileExtensionToContentTypeDefinition XXFileExtensionNameXXFileExtensionDefinition = null;
  }
}
