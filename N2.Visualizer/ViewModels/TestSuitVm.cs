using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using N2.Visualizer.Properties;

namespace N2.Visualizer.ViewModels
{
  class TestSuitVm : FullPathVm
  {
    public string                                   Name          { get; private set; }
    public ObservableCollection<GrammarDescriptor>  SynatxModules { get; private set; }
    public RuleDescriptor                           StartRule     { get; private set; }
    public ObservableCollection<TestVm>             Tests         { get; private set; }
    public string                                   TestSuitPath  { get; set; }

    private readonly string _rootPath;
    public XElement Xml { get { return Utils.MakeXml(_rootPath, SynatxModules, StartRule); } }


    public TestSuitVm(string rootPath, string testSuitPath)
      : base(testSuitPath)
    {
      _rootPath = rootPath;
      TestSuitPath = testSuitPath;
      var gonfigPath = Path.GetFullPath(Path.Combine(testSuitPath, "config.xml"));
      var root = XElement.Load(gonfigPath);

      var libs = root.Elements("Lib");

      var result =
        libs.Select(lib => Utils.LoadAssembly(Path.GetFullPath(Path.Combine(rootPath, lib.Attribute("Path").Value)))
          .Join(lib.Elements("SyntaxModule"),
            m => m.FullName,
            m => m.Attribute("Name").Value,
            (m, info) => new { Module = m, StartRule = GetStratRule(info.Attribute("StartRule"), m) }));


      SynatxModules  = new ObservableCollection<GrammarDescriptor>();

      foreach (var x in result.SelectMany(lib => lib))
      {
        SynatxModules.Add(x.Module);
        if (x.StartRule != null)
        {
          Debug.Assert(StartRule == null);
          StartRule = x.StartRule;
        }
      }

      Name = Path.GetFileName(testSuitPath);

      var tests = new ObservableCollection<TestVm>();

      foreach (var testPath in Directory.GetFiles(testSuitPath, "*.test").OrderBy(f => f))
        tests.Add(new TestVm(testPath, this));

      Tests = tests;
    }


    private static RuleDescriptor GetStratRule(XAttribute startRule, GrammarDescriptor m)
    {
      return startRule == null ? null : m.Rules.First(r => r.Name == startRule.Value);
    }
  }
}
