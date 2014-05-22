using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;

namespace JetBrains.Test
{
  class NitraDeclarationType : CompositeNodeType
  {
    public static NitraDeclarationType Instance = new NitraDeclarationType();

    public NitraDeclarationType()
      : base("NitraDeclarationType", 1)
    {
    }

    public override CompositeElement Create()
    {
      return new NitraFile();
    }
  }
}