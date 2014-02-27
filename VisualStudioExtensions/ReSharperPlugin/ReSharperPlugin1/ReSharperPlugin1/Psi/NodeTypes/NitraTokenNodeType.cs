using System;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.Text;

namespace JetBrains.Test
{
  internal abstract class NitraTokenNodeType : TokenNodeType
  {
    protected NitraTokenNodeType(string s, int index)
      : base(s, index)
    {
      //CSharpNodeTypeIndexer.Instance.Add(this, index);
    }

    public override LeafElementBase Create(IBuffer buffer, TreeOffset startOffset, TreeOffset endOffset)
    {
      throw new InvalidOperationException();
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
      get { return false; }
    }

    public override bool IsKeyword
    {
      get { return false; }
    }
  }
}