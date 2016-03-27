using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Nitra.ViewModels
{
  public class SolutionVm : ITestTreeNode
  {
    public ObservableCollection<TestSuiteVm> TestSuites { get; private set; }
    public bool IsDirty { get; private set; }
    public string SolutinFilePath { get; private set; }
    public string RootFolder { get; private set; }

    public SolutionVm(string solutinFilePath, string selectePath, string config)
    {
      var isSolutinFileExists = solutinFilePath != null && File.Exists(solutinFilePath);
      if (!isSolutinFileExists)
      {
        var message = "The '" + solutinFilePath + "' not exists.";
        Debug.Assert(isSolutinFileExists, message);
        // ReSharper disable once HeuristicUnreachableCode
        throw new ApplicationException(message);
      }

      SolutinFilePath = solutinFilePath;
      RootFolder = Path.GetDirectoryName(solutinFilePath);

      var suits   = File.ReadAllLines(solutinFilePath);
      var rootDir = Path.GetDirectoryName(solutinFilePath);
      Debug.Assert(rootDir != null, "rootDir != null");
      TestSuites   = new ObservableCollection<TestSuiteVm>();

      foreach (var aSuite in suits)
      {
        var suite = aSuite.Trim();
        
        if (string.IsNullOrEmpty(suite))
          continue;

        var testSuite = new TestSuiteVm(this, suite, config);
        
        if (selectePath != null)
        {
          if (testSuite.FullPath == selectePath)
            testSuite.IsSelected = true; // Прикольно что по другому фокус не изменить!
          else foreach (var test in testSuite.Tests)
            if (test.FullPath == selectePath)
              test.IsSelected = true;
        }
      }

      TestSuites.CollectionChanged += TestSuites_CollectionChanged;
      IsDirty = false;
    }

    public string Name { get { return Path.GetFileName(SolutinFilePath); } }

    void TestSuites_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
      IsDirty = true;
    }

    public void Save()
    {
      if (!IsDirty)
        return;

      var builder = new StringBuilder();

      foreach (var testSuiteVm in TestSuites)
        builder.AppendLine(testSuiteVm.Name);

      File.WriteAllText(SolutinFilePath, builder.ToString(), Encoding.UTF8);

      IsDirty = false;
    }

    public override string ToString()
    {
      return Name;
    }

    public string[] GetUnattachedTestSuites()
    {
      var dir = Path.GetDirectoryName(SolutinFilePath);
      return Directory.GetDirectories(dir ?? "").Select(Path.GetFileName).Except(TestSuites.Select(s => s.Name)).ToArray();
    }

    public ITestTreeNode Parent
    {
      get { return null; }
    }
  }
}
