using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.Text;
using JetBrains.UI.Icons;

namespace JetBrains.Test
{
  [ProjectFileType(typeof (DslFileType))]
  public class DslProjectFileLanguageService : ProjectFileLanguageService
  {
    public DslProjectFileLanguageService(DslFileType projectFileType)
      : base(projectFileType)
    {
    }

    protected override PsiLanguageType PsiLanguageType
    {
      get { return DslLanguage.Instance; }
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