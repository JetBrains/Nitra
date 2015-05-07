using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;

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
      get { return XXLanguageXXLanguage.Instance; }
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

  [LanguageDefinition(Name)]
  public class XXLanguageXXLanguage : KnownLanguage
  {
    private new const string Name = "XXLanguageXX";

    [UsedImplicitly]
    public static XXLanguageXXLanguage Instance;

    protected XXLanguageXXLanguage()
      : base(Name, Name)
    {
    }

    protected XXLanguageXXLanguage([NotNull] string name)
      : base(name, name)
    {
    }

    protected XXLanguageXXLanguage([NotNull] string name, [NotNull] string presentableName)
      : base(name, presentableName)
    {
    }
  }
}