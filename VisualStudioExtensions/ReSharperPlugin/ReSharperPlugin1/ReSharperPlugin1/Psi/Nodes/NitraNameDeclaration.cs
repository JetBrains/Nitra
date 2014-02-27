using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;

namespace JetBrains.Test
{
  internal class NitraNameDeclaration : NitraTokenElement
  {
    public NitraNameDeclaration(IPsiSourceFile sourceFile, string name, int start, int len) : base(name, start, len)
    {
    }

    public override NodeType NodeType
    {
      get { return NitraIdentifierNodeType.Instance; }
    }

    public override string ToString()
    {
      return "NameDeclaration " + myCachedOffsetData + ":" + GetText();
    }
  }
}