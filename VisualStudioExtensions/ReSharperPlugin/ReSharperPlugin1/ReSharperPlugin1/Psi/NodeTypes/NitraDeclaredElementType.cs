using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.UI.Icons;

namespace JetBrains.Test
{
  class NitraDeclaredElementType : DeclaredElementType
  {
    public static NitraDeclaredElementType Instance = new NitraDeclaredElementType();

    public NitraDeclaredElementType()
      : base("NitraDeclaredElementType")
    {
    }

    protected override IconId GetImage()
    {
      return null;
    }

    public override bool IsPresentable(PsiLanguageType language)
    {
      return false;
    }

    public override string PresentableName
    {
      get { throw new System.NotImplementedException(); }
    }

    protected override IDeclaredElementPresenter DefaultPresenter
    {
      get { throw new System.NotImplementedException(); }
    }
  }
}