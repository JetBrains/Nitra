using Nitra.Visualizer;
using Nitra.Visualizer.Annotations;

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Nitra.Visualizer.Serialization;
using System.Reflection;
using Nitra.ProjectSystem;
using File = System.IO.File;

namespace Nitra.ViewModels
{
  public class TestSuiteVm : FullPathVm, ITestTreeContainerNode
  {
    public static string ConfigFileName = "config.xml";

    public SolutionVm                              Solution                { get; private set; }
    public string                                  Name                    { get; private set; }
    public Language                                Language                { get; private set; }
    public ObservableCollection<GrammarDescriptor> DynamicExtensions       { get; private set; }
    public ObservableCollection<ITest>             Tests                   { get; private set; }
    public IEnumerable<ITest>                      Children                { get { return Tests; } }
    public string                                  TestSuitePath           { get; set; }
    public Exception                               Exception               { get; private set; }
    public TimeSpan                                TestTime                { get; private set; }
    public StatisticsTask.Container                Statistics              { get; private set; }
    public Assembly[]                              Assemblies              { get; private set; }
    public LibReference[]                          Libs                    { get; private set; }
    public bool                                    DisableSemanticAnalysis { get; }

    public string _hint;
    public override string Hint { get { return _hint; } }

    public static readonly Assembly[] NoAssembiles = new Assembly[0];

    readonly string _rootPath;

    public TestSuiteVm(SolutionVm solution, string name, string config)
      : base(solution, Path.Combine(solution.RootFolder, name))
    {
      Statistics = new StatisticsTask.Container("TestSuite", "Test Suite");
      string testSuitePath = base.FullPath;
      var rootPath = solution.RootFolder;
      Solution = solution;
      _rootPath = rootPath;
      TestSuitePath = testSuitePath;
      Language = Language.Instance;
      DynamicExtensions = new ObservableCollection<GrammarDescriptor>();
      Assemblies = NoAssembiles;

      var libs = new List<LibReference>
      {
        new FileLibReference(Nitra.Visualizer.Utils.NitraRuntimePath)
      };

      var configPath = Path.GetFullPath(Path.Combine(testSuitePath, ConfigFileName));

      try
      {
        var assemblyRelativePaths = new Dictionary<string, Assembly>();

        var languageAndExtensions = SerializationHelper.Deserialize(File.ReadAllText(configPath),
          path =>
          {
            var fullPath = Path.GetFullPath(Path.Combine(rootPath, path));
            Assembly result;
            if (!assemblyRelativePaths.TryGetValue(fullPath, out result))
              assemblyRelativePaths.Add(fullPath, result = Utils.LoadAssembly(fullPath, config));
            return result;
          });

        Language = languageAndExtensions.Item1;
        foreach (var ext in languageAndExtensions.Item2)
          DynamicExtensions.Add(ext);

        Assemblies = assemblyRelativePaths.Values.ToArray();

        libs.AddRange(languageAndExtensions.Item3);
        DisableSemanticAnalysis = languageAndExtensions.Item4;

        var indent = Environment.NewLine + "  ";
        var para = Environment.NewLine + Environment.NewLine;

        _hint = "Language:"          + indent + Language.FullName + para
              + "DynamicExtensions:" + indent + string.Join(indent, DynamicExtensions.Select(g => g.FullName)) + para
              + "Libraries:"         + indent + string.Join(indent, assemblyRelativePaths.Keys);
      }
      catch (FileNotFoundException ex)
      {
        TestState = TestState.Ignored;

        string additionMsg = null;

        if (ex.FileName.EndsWith("config.xml", StringComparison.OrdinalIgnoreCase))
          additionMsg = @"The configuration file (config.xml) does not exist in the test suite folder.";
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

      Libs = libs.ToArray();

      Name = Path.GetFileName(testSuitePath);

      var tests = new ObservableCollection<ITest>();

      if (Directory.Exists(testSuitePath))
      {
        var paths = Directory.GetFiles(testSuitePath, "*.test").Concat(Directory.GetDirectories(testSuitePath));
        var id = 0;
        foreach (var path in paths.OrderBy(f => f))
        {
          if (Directory.Exists(path))
            tests.Add(new TestFolderVm(path, this));
          else
            tests.Add(new TestVm(path, id, this));
          id++;
        }
      }
      else if (TestState != TestState.Ignored)
      {
        _hint = "The test suite folder '" + Path.GetDirectoryName(testSuitePath) + "'does not exist.";
        TestState = TestState.Ignored;
      }

      Tests = tests;
      solution.TestSuites.Add(this);
    }

    public string Xml { get { return Utils.MakeXml(_rootPath, Language, DynamicExtensions, Libs, DisableSemanticAnalysis); } }

    public RecoveryAlgorithm RecoveryAlgorithm { get; set; }

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
      var source = new SourceSnapshot(code);

      if (Language.StartRule == null)
        return null;

      try
      {
        var parseSession = new ParseSession(Language.StartRule,
          compositeGrammar:   Language.CompositeGrammar,
          completionPrefix:   completionPrefix,
          completionStartPos: completionStartPos,
          parseToEndOfString: true,
          dynamicExtensions:  DynamicExtensions,
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
      var xtml = Language.CompositeGrammar.ToHtml();
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
      var fullPath = TestFullPath(this.TestSuitePath);
      Solution.TestSuites.Remove(this);
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
