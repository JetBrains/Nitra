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

namespace XXNamespaceXX
{
  using JetBrains.ProjectModel;
  using JetBrains.ReSharper.Psi;
  using JetBrains.ReSharper.Psi.Parsing;
  using JetBrains.Text;
  using JetBrains.UI.Icons;
  
  [ProjectFileTypeDefinition(Name)]
  public class XXLanguageXXFileType : KnownProjectFileType
  {
    public new const string Name = "XXLanguageXX";
    public new static readonly XXLanguageXXFileType Instance = new XXLanguageXXFileType();

    private XXLanguageXXFileType()
      : base(Name, "XXLanguageXX", NitraFileExtensionsAndContentTypeDefinition.FileExtensions)
    {
    }

    protected XXLanguageXXFileType(string name)
      : base(name)
    {
    }

    protected XXLanguageXXFileType(string name, string presentableName)
      : base(name, presentableName)
    {
    }
  }

  [ProjectFileType(typeof(XXLanguageXXFileType))]
  public class XXLanguageXXProjectFileLanguageService : ProjectFileLanguageService
  {
    public XXLanguageXXProjectFileLanguageService(XXLanguageXXFileType projectFileType)
      : base(projectFileType)
    {
    }

    protected override PsiLanguageType PsiLanguageType
    {
      get { return null/*XXLanguageXXLanguage.Instance*/; }
    }

    public override IconId Icon
    {
      get
      {
        //return LexPluginSymbolThemedIcons.PsiFile.Id;
        return null;
      }
    }

    public override ILexerFactory GetMixedLexerFactory(ISolution solution, IBuffer buffer, IPsiSourceFile sourceFile = null)
    {
      return null;
    }
  }
}