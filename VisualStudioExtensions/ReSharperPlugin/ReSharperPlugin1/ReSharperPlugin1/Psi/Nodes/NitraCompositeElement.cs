using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.TextControl.DocumentMarkup;

[assembly: RegisterHighlighter(
  id: "ssss",
  EffectColor = "Red",
  EffectType = EffectType.SOLID_UNDERLINE,
  Layer = HighlighterLayer.SYNTAX,
  VSPriority = VSPriority.IDENTIFIERS)]

namespace JetBrains.Test
{

  internal abstract class NitraCompositeElement : CompositeElement, INitraAst
  {
    public override PsiLanguageType Language
    {
      get { return DslLanguage.Instance; }
    }
  }
}
