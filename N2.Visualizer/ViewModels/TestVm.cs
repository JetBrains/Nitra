using System.IO;

namespace N2.Visualizer.ViewModels
{
  class TestVm : FullPathVm
  {
    public string     TestPath { get; private set; }
    public TestSuitVm TestSuit { get; private set; }
    public string     Name     { get { return Path.GetFileNameWithoutExtension(TestPath); } }

    public string Code
    {
      get { return File.ReadAllText(TestPath); }
      set { File.WriteAllText(TestPath, value); }
    }

    public string Gold
    {
      get { return File.ReadAllText(Path.ChangeExtension(TestPath, ".gold")); }
      set { File.WriteAllText(Path.ChangeExtension(TestPath, ".gold"), value); }
    }

    public TestVm(string testPath, TestSuitVm testSuit)
      :base(testPath)
    {
      TestPath = testPath;
      TestSuit = testSuit;
    }
  }
}
