using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;

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