using Nitra.Declarations;
using Nitra.ProjectSystem;
using Nitra.Visualizer.Annotations;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IOFile = System.IO.File;

namespace Nitra.ViewModels
{
  public class TestVm : FullPathVm, ITest
  {
    public static readonly Guid TypingMsg = Guid.NewGuid(); 

    public string                   TestPath              { get; private set; }
    public FsFile<IAst>             File { get { return _file; } }
    private readonly TestFile       _file;

    public TestSuiteVm              TestSuite             { get; private set; }
    public string                   Name                  { get { return Path.GetFileNameWithoutExtension(TestPath); } }
    public string                   PrettyPrintResult     { get; private set; }
    public Exception                Exception             { get; private set; }
    public TimeSpan                 TestTime              { get; private set; }
    public StatisticsTask.Container Statistics            { get; private set; }
    public FileStatistics           FileStatistics        { get; private set; }
    public bool                     IsSingleFileTest       { get { return Parent is TestSuiteVm; } }

    private TestFolderVm _testFolder;

    private class TestFile : FsFile<IAst>
    {
      private readonly TestVm _test;
      public int _completionStartPos = -1;
      public string _completionPrefix = null;

      public TestFile([NotNull] TestVm test, Language language, FsProject<IAst> project, FileStatistics statistics)
        : base(test.TestPath, language, project, statistics)
      {
        if (test == null) throw new ArgumentNullException("test");
        _test = test;
      }

      protected override ParseSession GetParseSession()
      {
        var session = base.GetParseSession();
        session.CompletionStartPos = _completionStartPos;
        session.CompletionPrefix   = _completionPrefix;
        session.DynamicExtensions  = _test.TestSuite.DynamicExtensions;
        switch (_test.TestSuite.RecoveryAlgorithm)
        {
          case RecoveryAlgorithm.Smart:      session.OnRecovery = ParseSession.SmartRecovery; break;
          case RecoveryAlgorithm.Panic:      session.OnRecovery = ParseSession.PanicRecovery; break;
          case RecoveryAlgorithm.FirstError: session.OnRecovery = ParseSession.FirsrErrorRecovery; break;
        }
        return session;
      }

      public override SourceSnapshot GetSource()
      {
        return new SourceSnapshot(_test.Code, this);
      }

      public override int Length
      {
        get { return _test.Code.Length; }
      }
    }

    public TestVm(string testPath, ITestTreeNode parent)
      : base(parent, testPath)
    {
      _testFolder = parent as TestFolderVm;
      TestPath = testPath;
      TestSuite = _testFolder == null ? (TestSuiteVm)parent : _testFolder.TestSuite;
      
      if (_testFolder != null)
      {
        Statistics            = null;
        FileStatistics = new FileStatistics(
          _testFolder.ParsingStatistics.ReplaceSingleSubtask(Name),
          _testFolder.ParseTreeStatistics.ReplaceSingleSubtask(Name),
          _testFolder.AstStatistics.ReplaceSingleSubtask(Name),
          _testFolder.DependPropsStatistics);
        _file = new TestFile(this, TestSuite.Language, _testFolder.Project, FileStatistics);
      }
      else
      {
        Statistics            = new StatisticsTask.Container("Total");
        FileStatistics = new FileStatistics(
          Statistics.ReplaceSingleSubtask("Parsing"),
          Statistics.ReplaceSingleSubtask("ParseTree"),
          Statistics.ReplaceSingleSubtask("Ast", "AST Creation"),
          Statistics.ReplaceContainerSubtask("DependProps", "Dependent properties"));
        var solution = new FsSolution<IAst>();
        var project = new FsProject<IAst>(solution, Path.GetDirectoryName(testPath), TestSuite.Libs);
        _file = new TestFile(this, TestSuite.Language, project, FileStatistics);
      }

      if (TestSuite.TestState == TestState.Ignored)
        TestState = TestState.Ignored;

    }

    public override string Hint { get { return Code; } }

    private string _code;
    public string Code
    {
      get { return _code ?? (_code = IOFile.ReadAllText(TestPath)); }
      set
      {
        _code = value; this.File.ResetCache();
        Action f = () => { lock (this) IOFile.WriteAllText(TestPath, value); };
        f.BeginInvoke(null, null);
      }
    }

    public string Gold
    {
      get
      {
        var path = GolgPath;
        if (IOFile.Exists(path))
          return IOFile.ReadAllText(path);
        return "";
      }
      set { IOFile.WriteAllText(Path.ChangeExtension(TestPath, ".gold"), value); }
    }

    public string GolgPath
    {
      get { return Path.ChangeExtension(TestPath, ".gold"); }
    }


    [CanBeNull]
    public bool Run(RecoveryAlgorithm recoveryAlgorithm = RecoveryAlgorithm.Smart, int completionStartPos = -1, string completionPrefix = null)
    {
      var project = _file.Project;
      _file._completionStartPos = completionStartPos;
      _file._completionPrefix   = completionPrefix;
      _file.ResetCache();

      if (_file.Ast == null)
        return false;
      
      var tests = _testFolder == null ? (IEnumerable<TestVm>)new[] {this} : _testFolder.Tests;
      var files = tests.Select(t => t.File).ToArray();
      foreach (var file in files)
        file.DeepResetProperties();

      var projectSupport = _file.Ast as IProjectSupport;

      if (projectSupport != null)
        projectSupport.RefreshProject(project);
      else if (_testFolder != null)
        throw new InvalidOperationException("The '" + _file.Ast.GetType().Name +
                                            "' type must implement IProjectSupport, to be used in a multi-file test.");
      else
      {
        var context  = new DependentPropertyEvalContext();
        var evalHost = new ProjectEvalPropertiesHost(files);
        evalHost.EvalProperties(context);
      }

      return true;
    }

    public void CheckGold(RecoveryAlgorithm recoveryAlgorithm)
    {
      if (TestSuite.TestState == TestState.Ignored)
        return;

      
      var gold = Gold;
      var parseTree = (ParseTree)_file.GetParseTree();
      var prettyPrintResult = parseTree.ToString(PrettyPrintOptions.DebugIndent | PrettyPrintOptions.MissingNodes);
      PrettyPrintResult = prettyPrintResult;
      TestState = gold == prettyPrintResult ? TestState.Success : TestState.Failure;
    }

    public void Update([NotNull] string code, [NotNull] string gold)
    {
      IOFile.WriteAllText(TestPath, code);
      IOFile.WriteAllText(Path.ChangeExtension(TestPath, ".gold"), gold);
    }

    public void Remove()
    {
      var fullPath = Path.GetFullPath(this.TestPath);
      IOFile.Delete(fullPath);
      var goldFullPath = Path.ChangeExtension(fullPath, ".gold");
      if (IOFile.Exists(goldFullPath))
        IOFile.Delete(goldFullPath);
      var tests = TestSuite.Tests;
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
