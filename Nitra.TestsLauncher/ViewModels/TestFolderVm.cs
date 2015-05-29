using System.Collections.Generic;
using System.Collections.ObjectModel;
using Nitra.Visualizer.Annotations;

using System.IO;
using System.Linq;
using Nitra.Declarations;

namespace Nitra.ViewModels
{
  public class TestFolderVm : FullPathVm, ITest
  {
    public string                       TestPath          { get; private set; }
    public TestSuitVm                   TestSuit          { get; private set; }
    public string                       Name              { get { return Path.GetFileNameWithoutExtension(TestPath); } }
    public ObservableCollection<TestVm> Tests             { get; private set; }
    //public ObservableCollection<IAst>   CompilationUnits  { get; private set; }

    public TestFolderVm(string testPath, TestSuitVm testSuit)
      :base(testPath)
    {
      TestPath = testPath;
      TestSuit = testSuit;
      if (TestSuit.TestState == TestState.Ignored)
        TestState = TestState.Ignored;

      string testSuitPath = base.FullPath;
      var tests = new ObservableCollection<TestVm>();

      var paths = Directory.GetFiles(testSuitPath, "*.test");
      foreach (var path in paths.OrderBy(f => f))
        tests.Add(new TestVm(path, TestSuit));

      Tests = tests;
    }

    public override string Hint { get { return "TestFolder"; } }

    public IParseResult[] Run(RecoveryAlgorithm recoveryAlgorithm)
    {
      List<IParseResult> results = new List<IParseResult>();
      foreach (var test in Tests)
        results.Add(test.Run(recoveryAlgorithm));
      return results.ToArray();
    }

    public void Update([NotNull] string code, [NotNull] string gold)
    {
    }

    public void Remove()
    {
    }

    public override string ToString()
    {
      return Name;
    }
  }
}
