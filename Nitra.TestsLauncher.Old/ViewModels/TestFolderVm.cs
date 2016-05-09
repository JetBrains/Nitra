using System.Collections.Generic;
using System.Collections.ObjectModel;
using Nitra.Visualizer.Annotations;

using System.IO;
using System.Linq;
using Nitra.Declarations;
using Nitra.ProjectSystem;

namespace Nitra.ViewModels
{
  public class TestFolderVm : FullPathVm, ITest, ITestTreeContainerNode
  {
    public string                       TestPath          { get; private set; }
    public TestSuiteVm                   TestSuite          { get; private set; }
    public string                       Name              { get { return Path.GetFileNameWithoutExtension(TestPath); } }
    public ObservableCollection<TestVm> Tests             { get; private set; }
    public IEnumerable<ITest>           Children          { get { return Tests; } }
    //public ObservableCollection<IAst>   CompilationUnits  { get; private set; }

    public TestFolderVm(string testPath, TestSuiteVm testSuite)
      : base(testSuite, testPath)
    {
      var solution = new FsSolution<IAst>();
      this.Project = new FsProject<IAst>(solution, testPath, testSuite.Libs.Select(
        lib =>
        {
          var file = lib as FileLibReference;
          if (file == null || Path.IsPathRooted(file.Path))
            return lib;

          return new FileLibReference(Path.Combine(@"..", file.Path));
        }));

      Statistics            = new StatisticsTask.Container("Total");
      ParsingStatistics     = Statistics.ReplaceContainerSubtask("Parsing");
      ParseTreeStatistics   = Statistics.ReplaceContainerSubtask("ParseTree");
      AstStatistics         = Statistics.ReplaceContainerSubtask("Ast", "AST Creation");
      DependPropsStatistics = Statistics.ReplaceContainerSubtask("DependProps", "Dependent properties");

      TestPath = testPath;
      TestSuite = testSuite;
      if (TestSuite.TestState == TestState.Ignored)
        TestState = TestState.Ignored;

      string testSuitePath = base.FullPath;
      var tests = new ObservableCollection<TestVm>();

      var paths = Directory.GetFiles(testSuitePath, "*.test");
      var id = 0;
      foreach (var path in paths.OrderBy(f => f))
      {
        tests.Add(new TestVm(path, id, this));
        id++;
      }

      Tests = tests;
    }

    public override string Hint { get { return "TestFolder"; } }

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

    public StatisticsTask.Container Statistics            { get; private set; }
    public StatisticsTask.Container ParsingStatistics     { get; private set; }
    public StatisticsTask.Container ParseTreeStatistics   { get; private set; }
    public StatisticsTask.Container AstStatistics         { get; private set; }
    public StatisticsTask.Container DependPropsStatistics { get; private set; }

    public FsProject<IAst> Project
    {
      get; private set; }

    public void CalcDependProps(TestVm testVm)
    {
      
    }
  }
}
