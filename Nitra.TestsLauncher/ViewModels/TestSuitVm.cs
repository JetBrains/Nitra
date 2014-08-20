using Nitra.DebugStrategies;
using Nitra.Visualizer;
using Nitra.Visualizer.Annotations;

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Nitra.ViewModels
{
  public class TestSuitVm : FullPathVm
  {
    public string                                   Name          { get; private set; }
    public ObservableCollection<GrammarDescriptor>  SynatxModules { get; private set; }
    public StartRuleDescriptor                      StartRule     { get; private set; }
    public ObservableCollection<TestVm>             Tests         { get; private set; }
    public string                                   TestSuitPath  { get; set; }
    public Exception                                Exception     { get; private set; }

    public string _hint;
    public override string Hint { get { return _hint; } }

    public readonly Recovery Recovery = new Recovery();

    readonly string _rootPath;
    ParserHost _parserHost;
    private CompositeGrammar _compositeGrammar;

    public XElement Xml { get { return Utils.MakeXml(_rootPath, SynatxModules, StartRule); } }


    public TestSuitVm(string rootPath, string testSuitPath, string config)
      : base(testSuitPath)
    {
      _rootPath = rootPath;
      TestSuitPath = testSuitPath;
      SynatxModules = new ObservableCollection<GrammarDescriptor>();

      var gonfigPath = Path.GetFullPath(Path.Combine(testSuitPath, "config.xml"));

      try
      {
        var root = XElement.Load(gonfigPath);
        var libs = root.Elements("Lib").ToList();
        var result =
          libs.Select(lib => Utils.LoadAssembly(Path.GetFullPath(Path.Combine(rootPath, lib.Attribute("Path").Value)), config)
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
        var name = StartRule == null ? "" : StartRule.Name;

        var indent = Environment.NewLine + "  ";
        var para = Environment.NewLine + Environment.NewLine;

        _hint = "Libraries:" + indent + string.Join(indent, libs.Select(lib => Utils.UpdatePathForConfig(lib.Attribute("Path").Value, config))) + para
               + "Syntax modules:" + indent + string.Join(indent, SynatxModules.Select(m => m.FullName)) + para
               + "Start rule:" + indent + name;
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

      var tests = new ObservableCollection<TestVm>();

      foreach (var testPath in Directory.GetFiles(testSuitPath, "*.test").OrderBy(f => f))
        tests.Add(new TestVm(testPath, this));

      Tests = tests;

      //TestState = TestState.Success;
    }


    private static StartRuleDescriptor GetStratRule(XAttribute startRule, GrammarDescriptor m)
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
    public ParseResult Run([NotNull] string code, [CanBeNull] string gold, RecoveryStrategy recoveryStrategy)
    {
      if (_parserHost == null)
      {
        _parserHost = new ParserHost();
        _compositeGrammar = _parserHost.MakeCompositeGrammar(SynatxModules);
      }
      var source = new SourceSnapshot(code);

      if (StartRule == null)
        return null;

      try
      {
        var res = _parserHost.DoParsing(source, _compositeGrammar, StartRule, recoveryStrategy);
        this.Exception = null;
        return res;
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
  }
}
