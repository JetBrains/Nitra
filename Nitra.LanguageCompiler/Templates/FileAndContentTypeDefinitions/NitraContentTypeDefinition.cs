using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

using System.ComponentModel.Composition;
using System.Windows.Media;

namespace XXNamespaceXX
{
  internal static partial class NitraFileExtensionsAndContentTypeDefinition
  {
    [Export]
    [Name("XXLanguageXX")]
    [BaseDefinition("text")]
    public static ContentTypeDefinition XXLanguageXXContentTypeDefinition = null;

    internal static string[] FileExtensions = { "XXFileExtensionsXX" };
  }
}
