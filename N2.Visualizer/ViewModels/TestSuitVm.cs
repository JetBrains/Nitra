using System;
using N2.DebugStrategies;
using N2.Internal;
using N2.Visualizer.Annotations;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace N2.Visualizer.ViewModels
{
  class TestSuitVm : FullPathVm
  {
    public string                                   Name          { get; private set; }
    public ObservableCollection<GrammarDescriptor>  SynatxModules { get; private set; }
    public RuleDescriptor                           StartRule     { get; private set; }
    public ObservableCollection<TestVm>             Tests         { get; private set; }
    public string                                   TestSuitPath  { get; set; }

    public string _hint;
    public override string Hint { get { return _hint; } }

    public readonly Recovery Recovery = new Recovery();

    readonly string _rootPath;
    ParserHost _parserHost;
    private CompositeGrammar _compositeGrammar;

    public XElement Xml { get { return Utils.MakeXml(_rootPath, SynatxModules, StartRule); } }


    public TestSuitVm(string rootPath, string testSuitPath)
      : base(testSuitPath)
    {
      _rootPath = rootPath;
      TestSuitPath = testSuitPath;
      var gonfigPath = Path.GetFullPath(Path.Combine(testSuitPath, "config.xml"));
      var root = XElement.Load(gonfigPath);

      SynatxModules = new ObservableCollection<GrammarDescriptor>();

      try
      {
        var libs = root.Elements("Lib").ToList();
        var result =
          libs.Select(lib => Utils.LoadAssembly(Path.GetFullPath(Path.Combine(rootPath, lib.Attribute("Path").Value)))
            .Join(lib.Elements("SyntaxModule"),
              m => m.FullName,
              m => m.Attribute("Name").Value,
              (m, info) => new { Module = m, StartRule = GetStratRule(info.Attribute("StartRule"), m) }));



        foreach (var x in result.SelectMany(lib => lib))
        {
          SynatxModules.Add(x.Module);
          if (x.StartRule != null)
          {
            Debug.Assert(StartRule == null);
            StartRule = x.StartRule;
          }
        }
        var indent = Environment.NewLine + "  ";
        var para = Environment.NewLine + Environment.NewLine;

        _hint = "Libraries:" + indent + string.Join(indent, libs.Select(lib => lib.Attribute("Path").Value)) + para
               + "Syntax modules:" + indent + string.Join(indent, SynatxModules.Select(m => m.FullName)) + para
               + "Start rule:" + indent + StartRule.Name;
      }
      catch (Exception ex)
      {
        TestState = TestState.Ignored;
        _hint = "Failed to load test suite:" + Environment.NewLine + ex.Message;
      }

      Name = Path.GetFileName(testSuitPath);

      var tests = new ObservableCollection<TestVm>();

      foreach (var testPath in Directory.GetFiles(testSuitPath, "*.test").OrderBy(f => f))
        tests.Add(new TestVm(testPath, this));

      Tests = tests;

      //TestState = TestState.Success;
    }


    private static RuleDescriptor GetStratRule(XAttribute startRule, GrammarDescriptor m)
    {
      return startRule == null ? null : m.Rules.First(r => r.Name == startRule.Value);
    }


    internal void TestStateChanged()
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
    public Parser Run([NotNull] string code, [CanBeNull] string gold)
    {
      if (_parserHost == null)
      {
        _parserHost = new ParserHost(this.Recovery.Strategy);
        _compositeGrammar = _parserHost.MakeCompositeGrammar(SynatxModules);
      }
      var source = new SourceSnapshot(code);

      if (StartRule == null)
        return null;

      var simpleRule = StartRule as SimpleRuleDescriptor;

      this.Recovery.Init();

      if (simpleRule != null)
        return _parserHost.DoParsing(source, _compositeGrammar, simpleRule);
      else
        return _parserHost.DoParsing(source, _compositeGrammar, (ExtensibleRuleDescriptor)StartRule);
    }
  }
}
