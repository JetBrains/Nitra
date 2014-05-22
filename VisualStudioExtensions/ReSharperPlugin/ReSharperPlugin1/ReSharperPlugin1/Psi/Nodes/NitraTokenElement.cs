using System.Diagnostics;
using System.Text;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Text;

namespace JetBrains.Test
{
  abstract class NitraTokenElement : LeafElementBase, ITokenNode, INitraAst
  {
    protected readonly string myText;

    protected NitraTokenElement(string name, int start, int len)
    {
      this.myText = name;
      myCachedOffsetData = start;
      Debug.Assert(myCachedOffsetData == start);
      Debug.Assert(GetText() == name);
      Debug.Assert(GetTextLength() == len);
    }

    public sealed override int GetTextLength()
    {
      return this.myText.Length;
    }

    public sealed override string GetText()
    {
      return this.myText;
    }

    public sealed override StringBuilder GetText(StringBuilder to)
    {
      to.Append(this.GetText());
      return to;
    }

    public sealed override IBuffer GetTextAsBuffer()
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