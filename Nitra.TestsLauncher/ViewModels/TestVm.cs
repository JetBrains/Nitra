using System;
using Nitra.Visualizer.Annotations;

using System.IO;

namespace Nitra.ViewModels
{
  public class TestVm : FullPathVm, ITest
  {
    public string       TestPath          { get; private set; }
    public TestSuitVm   TestSuit          { get; private set; }
    public string       Name              { get { return Path.GetFileNameWithoutExtension(TestPath); } }
    public IParseResult Result            { get; private set; }
    public string       PrettyPrintResult { get; private set; }
    public Exception    Exception         { get; private set; }
    public TimeSpan     TestTime          { get; private set; }

    private TestFolderVm _testFolder;

    public TestVm(string testPath, TestSuitVm testSuit, TestFolderVm testFolder = null)
      : base(testPath)
    {
      _testFolder = testFolder;
      TestPath = testPath;
      TestSuit = testSuit;
      if (TestSuit.TestState == TestState.Ignored)
        TestState = TestState.Ignored;
    }

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

    [CanBeNull]
    public IParseResult Run([CanBeNull] string code = null, StatisticsTask.Container statistics = null, RecoveryAlgorithm recoveryAlgorithm = RecoveryAlgorithm.Smart, int completionStartPos = -1, string completionPrefix = null)
    {
      if (code == null)
        code = this.Code;

      var timer = System.Diagnostics.Stopwatch.StartNew();
      try
      {
        if (statistics == null)
          statistics = new StatisticsTask.Container("Total", "Total");

        var parseSession = new ParseSession(TestSuit.StartRule,
          compositeGrammar: TestSuit.CompositeGrammar,
          completionPrefix: completionPrefix,
          completionStartPos: completionStartPos,
          parseToEndOfString: true,
          dynamicExtensions: TestSuit.AllSynatxModules,
          statistics: statistics);
        switch (recoveryAlgorithm)
        {
          case RecoveryAlgorithm.Smart: parseSession.OnRecovery = ParseSession.SmartRecovery; break;
          case RecoveryAlgorithm.Panic: parseSession.OnRecovery = ParseSession.PanicRecovery; break;
          case RecoveryAlgorithm.FirstError: parseSession.OnRecovery = ParseSession.FirsrErrorRecovery; break;
        }

        var source = new SourceSnapshot(code);
        var parseResult = parseSession.Parse(source);
        this.Exception = null;
        this.TestTime = timer.Elapsed;
        return parseResult;
      }
      catch (Exception ex)
      {
        this.Exception = ex;
        this.TestTime = timer.Elapsed;
        return null;
      }
    }

    public IParseResult Run(RecoveryAlgorithm recoveryAlgorithm)
    {
      if (TestSuit.TestState == TestState.Ignored)
        return null;

      var result = TestSuit.Run(Code, Gold, recoveryAlgorithm: recoveryAlgorithm);
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
