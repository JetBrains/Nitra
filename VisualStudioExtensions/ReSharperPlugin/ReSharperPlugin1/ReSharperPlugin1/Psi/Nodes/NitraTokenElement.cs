using System.Text;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Text;

namespace JetBrains.Test
{
  abstract class NitraTokenElement : LeafElementBase, ITokenNode
  {
    private readonly string myText;

    public NitraTokenElement(string name, int start, int len)
    {
      this.myText = name;
      myCachedOffsetData = start;
    }

    public override int GetTextLength()
    {
      return this.myText.Length;
    }

    public override string GetText()
    {
      return this.myText;
    }

    public override StringBuilder GetText(StringBuilder to)
    {
      to.Append(this.GetText());
      return to;
    }

    public override IBuffer GetTextAsBuffer()
    {
      return new StringBuffer(this.GetText());
    }

    public override PsiLanguageType Language
    {
      get { return DslLanguage.Instance; }
    }

    public TokenNodeType GetTokenType()
    {
      return (TokenNodeType)this.NodeType;
    }
  }
}