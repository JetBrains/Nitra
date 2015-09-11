using Nitra.Visualizer;
using Nitra.Visualizer.Annotations;

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Nitra.ViewModels
{
  public class TestSuitVm : FullPathVm, ITestTreeContainerNode
  {
    public SolutionVm Solution { get; private set; }
    public string Name { get; private set; }
    public ObservableCollection<GrammarDescriptor>  SynatxModules    { get; private set; }
    public StartRuleDescriptor                      StartRule        { get; private set; }
    public ObservableCollection<ITest>              Tests            { get; private set; }
    public IEnumerable<ITest>                       Children         { get { return Tests; } }
    public string                                   TestSuitPath     { get; set; }
    public Exception                                Exception        { get; private set; }
    public TimeSpan                                 TestTime         { get; private set; }
    public StatisticsTask.Container                 Statistics       { get; private set; }
    public string _hint;
    public override string Hint { get { return _hint; } }
    public string[] LibPaths { get; private set; }
    public IEnumerable<GrammarDescriptor> AllSynatxModules { get; private set; }
    public string Language { get; private set; }

    readonly string _rootPath;
    private CompositeGrammar _compositeGrammar;

    public TestSuitVm(SolutionVm solution, string name, string config)
      : base(solution, Path.Combine(solution.RootFolder, name))
    {
      Statistics = new StatisticsTask.Container("TestSuite", "Test Suite");
      string testSuitPath = base.FullPath;
      var rootPath = solution.RootFolder;
      Solution = solution;
      _rootPath = rootPath;
      TestSuitPath = testSuitPath;
      SynatxModules = new ObservableCollection<GrammarDescriptor>();

      var gonfigPath = Path.GetFullPath(Path.Combine(testSuitPath, "config.xml"));

      try
      {
        var root = XElement.Load(gonfigPath);
        var libs = root.Elements("Lib").ToList();
        var language = root.Attribute("Language");
        Language = language == null ? "<none>" : language.Value;
        LibPaths = libs.Where(lib => lib.Attribute("Path") != null).Select(lib => lib.Attribute("Path").Value).ToArray();
        AllSynatxModules = LibPaths.SelectMany(lib => Utils.LoadAssembly(Path.GetFullPath(Path.Combine(rootPath, lib)), config)).ToArray();
        var result =
          libs.Select(lib => Utils.LoadAssembly(Path.GetFullPath(Path.Combine(rootPath, lib.Attribute("Path").Value)), config)
            .Join(lib.Elements("SyntaxModule"),
              m => m.FullName,
              m => m.Attribute("Name").Value,
              (m, info) => new { Module = m, StartRule = GetStartRule(info.Attribute("StartRule"), m) }));



        foreach (var x in result.SelectMany(lib => lib))
        {
          SynatxModules.Add(x.Module);
          if (x.StartRule != null)
          {
            Debug.Assert(StartRule == null);
            StartRule = x.StartRule;
          }
        }
        var startRuleName = StartRule == null ? "" : StartRule.Name;

        var indent = Environment.NewLine + "  ";
        var para = Environment.NewLine + Environment.NewLine;

        _hint = "Libraries:" + indent + string.Join(indent, libs.Select(lib => Utils.UpdatePathForConfig(lib.Attribute("Path").Value, config))) + para
               + "Syntax modules:" + indent + string.Join(indent, SynatxModules.Select(m => m.FullName)) + para
               + "Start rule:" + indent + startRuleName;
      }
      catch (FileNotFoundException ex)
      {
        TestState = TestState.Ignored;

        string additionMsg = null;

        if (ex.FileName.EndsWith("config.xml", StringComparison.OrdinalIgnoreCase))
          additionMsg = @"The configuration file (config.xml) not exists in the test suit folder.";
        else if (ex.FileName.EndsWith("Nitra.Runtime.dll", StringComparison.OrdinalIgnoreCase))
          additionMsg = @"Try to recompile the parser.";

        if (additionMsg != null)
          additionMsg = Environment.NewLine + Environment.NewLine + additionMsg;

        _hint = "Failed to load test suite:" + Environment.NewLine + ex.Message + additionMsg;
      }
      catch (Exception ex)
      {
        TestState = TestState.Ignored;
        _hint = "Failed to load test suite:" + Environment.NewLine + ex.GetType().Name + ":" + ex.Message;
      }

      Name = Path.GetFileName(testSuitPath);

      var tests = new ObservableCollection<ITest>();

      if (Directory.Exists(testSuitPath))
      {
        var paths = Directory.GetFiles(testSuitPath, "*.test").Concat(Directory.GetDirectories(testSuitPath));
        foreach (var path in paths.OrderBy(f => f))
          if (Directory.Exists(path))
            tests.Add(new TestFolderVm(path, this));
          else
            tests.Add(new TestVm(path, this));
      }
      else if (TestState != TestState.Ignored)
      {
        _hint = "The test suite folder '" + Path.GetDirectoryName(testSuitPath) + "' not exists.";
        TestState = TestState.Ignored;
      }

      Tests = tests;
      solution.TestSuits.Add(this);
    }

    public CompositeGrammar CompositeGrammar { get { return _compositeGrammar = ParserHost.Instance.MakeCompositeGrammar(SynatxModules); } }

    public XElement Xml { get { return Utils.MakeXml(_rootPath, SynatxModules, StartRule, Language); } }

    public RecoveryAlgorithm RecoveryAlgorithm { get; set; }

    private static StartRuleDescriptor GetStartRule(XAttribute startRule, GrammarDescriptor m)
    {
      return startRule == null ? null : m.Rules.OfType<StartRuleDescriptor>().First(r => r.Name == startRule.Value);
    }


    public void TestStateChanged()
    {
      if (this.TestState == TestState.Ignored)
        return;

      var hasNotRunnedTests = false;

      foreach (var test in Tests)
      {

        if (test.TestState == TestState.Failure)
        {
          this.TestState = TestState.Failure;
          return;
        }

        if (!hasNotRunnedTests && test.TestState != TestState.Success)
          hasNotRunnedTests = true;
      }

      this.TestState = hasNotRunnedTests ? TestState.Skipped : TestState.Success;
    }

    [CanBeNull]
    public IParseResult Run([NotNull] string code, [CanBeNull] string gold = null, int completionStartPos = -1, string completionPrefix = null, RecoveryAlgorithm recoveryAlgorithm = RecoveryAlgorithm.Smart)
    {
      _compositeGrammar = ParserHost.Instance.MakeCompositeGrammar(SynatxModules);

      var source = new SourceSnapshot(code);

      if (StartRule == null)
        return null;

      try
      {
        var parseSession = new ParseSession(StartRule,
          compositeGrammar:   _compositeGrammar,
          completionPrefix:   completionPrefix,
          completionStartPos: completionStartPos,
          parseToEndOfString: true,
          dynamicExtensions:  AllSynatxModules,
          statistics:         Statistics);
        switch (recoveryAlgorithm)
        {
          case RecoveryAlgorithm.Smart: parseSession.OnRecovery = ParseSession.SmartRecovery; break;
          case RecoveryAlgorithm.Panic: parseSession.OnRecovery = ParseSession.PanicRecovery; break;
          case RecoveryAlgorithm.FirstError: parseSession.OnRecovery = ParseSession.FirsrErrorRecovery; break;
        }
        var parseResult = parseSession.Parse(source);
        this.Exception = null;
        return parseResult;
      }
      catch (Exception ex)
      {
        this.Exception = ex;
        return null;
      }
    }

    public void ShowGrammar()
    {
      var xtml = _compositeGrammar.ToHtml();
      var filePath = Path.ChangeExtension(Path.GetTempFileName(), ".html");
      xtml.Save(filePath, SaveOptions.DisableFormatting);
      Process.Start(filePath);
    }

    public override string ToString()
    {
      return Name;
    }

    public void Remove()
    {
      var fullPath = TestFullPath(this.TestSuitPath);
      Solution.TestSuits.Remove(this);
      Solution.Save();
      if (Directory.Exists(fullPath))
        Directory.Delete(fullPath, true);
    }

    private static string TestFullPath(string path)
    {
      return Path.GetFullPath(path);
    }
  }
}
