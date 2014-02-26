using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Tree;

namespace JetBrains.Test
{
  class NitraWhitespaceElement : NitraTokenElement, ITokenNode
  {
    protected NitraWhitespaceElement(string name, int start, int len) : base(name, start, len)
    {
    }

    public override NodeType NodeType
    {
      get { throw new System.NotImplementedException(); }
    }
  }
}