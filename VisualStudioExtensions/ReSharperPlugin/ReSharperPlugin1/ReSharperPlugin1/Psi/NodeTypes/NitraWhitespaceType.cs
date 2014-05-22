using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;

namespace JetBrains.Test
{
  class NitraWhitespaceType : NitraTokenNodeType
  {
    public static NitraWhitespaceType Instance = new NitraWhitespaceType();

    public NitraWhitespaceType()
      : base("WHITE_SPACE", 1)
    {
    }

    public override bool   IsWhitespace         { get { return true; } }
    public override string TokenRepresentation  { get { return " "; } }
    public override bool   IsFiltered           { get { return true; } }
  }
}