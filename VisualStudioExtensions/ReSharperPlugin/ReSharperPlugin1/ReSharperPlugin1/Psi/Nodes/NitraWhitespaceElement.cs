using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Tree;

namespace JetBrains.Test
{
  class NitraWhitespaceElement : NitraTokenElement, INitraAst
  {
    public NitraWhitespaceElement(string name, int start, int len) : base(name, start, len)
    {
    }

    public override NodeType NodeType
    {
      get { return NitraWhitespaceType.Instance; }
    }

    public override string ToString()
    {
      return "Whitespace " + myCachedOffsetData + ":" + GetText();
    }

    public override bool IsFiltered()
    {
      return true;
    }
  }
}