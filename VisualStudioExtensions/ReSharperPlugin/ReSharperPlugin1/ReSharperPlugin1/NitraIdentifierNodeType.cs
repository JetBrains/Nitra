using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.Text;

namespace JetBrains.Test
{
  class NitraIdentifierNodeType : TokenNodeType
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

    public override bool IsWhitespace
    {
      get { return false; }
    }

    public override bool IsComment
    {
      get { return false; }
    }

    public override bool IsStringLiteral
    {
      get { return false; }
    }

    public override bool IsConstantLiteral
    {
      get { return false; }
    }

    public override bool IsIdentifier
    {
      get { return true; }
    }

    public override bool IsKeyword
    {
      get { return false; }
    }

    public override string TokenRepresentation
    {
      get { return "Identifier"; }
    }
  }
}