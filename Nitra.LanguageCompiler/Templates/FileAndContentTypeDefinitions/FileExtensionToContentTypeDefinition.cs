using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

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
