using System.IO;
using N2.Internal;

namespace N2.Visualizer.ViewModels
{
  class TestVm : FullPathVm
  {
    public string     TestPath          { get; private set; }
    public TestSuitVm TestSuit          { get; private set; }
    public string     Name              { get { return Path.GetFileNameWithoutExtension(TestPath); } }
    public Parser     Result            { get; private set; }
    public string     PrettyPrintResult { get; private set; }

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

    public Parser Run()
    {
      var result = TestSuit.Run(Code, Gold);
      
      var gold = Gold;
      var ast = result.CreateAst();
      var prettyPrintResult = ast.ToString(PrettyPrintOptions.DebugIndent | PrettyPrintOptions.MissingNodes);

      TestState = gold == prettyPrintResult ? TestState.Success : TestState.Failure;
      PrettyPrintResult = prettyPrintResult;
      // TODO: Сделать отображение расхождений в влучае, если TestState.Failure

      Result = result;
      return result;
    }
  }
}
