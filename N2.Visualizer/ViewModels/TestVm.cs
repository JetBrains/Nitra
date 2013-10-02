using System;
using System.Collections.Generic;
using System.IO;
using N2.Internal;
using N2.Visualizer.Annotations;
using Nemerle.Diff;

namespace N2.Visualizer.ViewModels
{
  class TestVm : FullPathVm
  {
    public string     TestPath          { get; private set; }
    public TestSuitVm TestSuit          { get; private set; }
    public string     Name              { get { return Path.GetFileNameWithoutExtension(TestPath); } }
    public ParseResult     Result            { get; private set; }
    public string     PrettyPrintResult { get; private set; }

    public override string Hint { get { return Code; } }

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
      if (TestSuit.TestState == TestState.Ignored)
        TestState = TestState.Ignored;
    }

    public ParseResult Run(RecoveryStrategy recoveryStrategy)
    {
      if (TestSuit.TestState == TestState.Ignored)
        return null;

      var result = TestSuit.Run(Code, Gold, recoveryStrategy);
      if (result == null)
        return null;

      var gold = Gold;
      var ast = result.CreateAst();
      var prettyPrintResult = ast.ToString(PrettyPrintOptions.DebugIndent | PrettyPrintOptions.MissingNodes);
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
  }
}
