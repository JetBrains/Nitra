using System.IO;

namespace N2.Visualizer.ViewModels
{
  class TestVm
  {
    public string TestPath  { get; private set; }

    public string Name      { get { return Path.GetFileNameWithoutExtension(TestPath); } }

    public TestVm(string path)
    {
      TestPath = path;
    }
  }
}
