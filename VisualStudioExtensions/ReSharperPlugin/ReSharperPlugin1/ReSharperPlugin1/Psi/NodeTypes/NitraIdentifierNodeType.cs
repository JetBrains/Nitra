using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.Text;

namespace JetBrains.Test
{
  class NitraIdentifierNodeType : NitraTokenNodeType
  {
    public static NitraIdentifierNodeType Instance = new NitraIdentifierNodeType();

    public NitraIdentifierNodeType()
      : base("NitraIdentifierNodeType", 2)
    {
    }

    public override LeafElementBase Create(IBuffer buffer, TreeOffset startOffset, TreeOffset endOffset)
    {
      throw new System.NotImplementedException();
    }

    public override bool IsIdentifier
    {
      get { return true; }
    }

    public override string TokenRepresentation
    {
      get { return "Identifier"; }
    }
  }
}