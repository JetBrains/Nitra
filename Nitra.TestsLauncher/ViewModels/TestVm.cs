using Nitra.Visualizer.Annotations;

using System.IO;

namespace Nitra.ViewModels
{
  public class TestVm : FullPathVm
  {
    public string       TestPath          { get; private set; }
    public TestSuitVm   TestSuit          { get; private set; }
    public string       Name              { get { return Path.GetFileNameWithoutExtension(TestPath); } }
    public IParseResult Result            { get; private set; }
    public string       PrettyPrintResult { get; private set; }

    public override string Hint { get { return Code; } }

    public string Code
    {
      get { return File.ReadAllText(TestPath); }
      set { File.WriteAllText(TestPath, value); }
    }

    public string Gold
    {
      get
      {
        try
        {
          return File.ReadAllText(Path.ChangeExtension(TestPath, ".gold"));
        }
        catch (FileNotFoundException)
        {
          return "";
        }
      }
      set { File.WriteAllText(Path.ChangeExtension(TestPath, ".gold"), value); }
    }

    public TestVm(string testPath, TestSuitVm testSuit)
      :base(testPath)
    {
      TestPath = testPath;
      TestSuit = testSuit;
      if (TestSuit.TestState == TestState.Ignored)
        TestState = TestState.Ignored;
    }

    public IParseResult Run()
    {
      if (TestSuit.TestState == TestState.Ignored)
        return null;

      var result = TestSuit.Run(Code, Gold);
      if (result == null)
        return null;

      var gold = Gold;
      var parseTree = result.CreateParseTree();
      var prettyPrintResult = parseTree.ToString(PrettyPrintOptions.DebugIndent | PrettyPrintOptions.MissingNodes);
      PrettyPrintResult = prettyPrintResult;
      TestState = gold == prettyPrintResult ? TestState.Success : TestState.Failure;
      Result = result;
      return result;
    }

    public void Update([NotNull] string code, [NotNull] string gold)
    {
      File.WriteAllText(TestPath, code);
      File.WriteAllText(Path.ChangeExtension(TestPath, ".gold"), gold);
    }

    public void Remove()
    {
      var fullPath = Path.GetFullPath(this.TestPath);
      File.Delete(fullPath);
      var goldFullPath = Path.ChangeExtension(fullPath, ".gold");
      if (File.Exists(goldFullPath))
        File.Delete(goldFullPath);
      var tests = TestSuit.Tests;
      var index = tests.IndexOf(this);
      tests.Remove(this);
      if (tests.Count > 0)
        tests[index].IsSelected = true;
    }

    public override string ToString()
    {
      return Name;
    }
  }
}
