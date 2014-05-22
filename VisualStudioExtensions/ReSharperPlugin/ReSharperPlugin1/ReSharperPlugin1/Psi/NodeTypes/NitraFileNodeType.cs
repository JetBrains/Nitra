using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;

namespace JetBrains.Test
{
  class NitraFileNodeType : CompositeNodeType
  {
    public static NitraFileNodeType Instance = new NitraFileNodeType();

    public NitraFileNodeType()
      : base("NitraFileNodeType", 1)
    {
    }

    public override CompositeElement Create()
    {
      return new NitraFile();
    }
  }
}