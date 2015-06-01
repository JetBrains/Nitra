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
    public ObservableCollection<TestSuitVm> TestSuits { get; private set; }
    public bool IsDirty { get; private set; }
    public string SolutinFilePath { get; private set; }
    public string RootFolder { get; private set; }

    public SolutionVm(string solutinFilePath, string selectePath, string config, ICompilerMessages compilerMessages)
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
      TestSuits   = new ObservableCollection<TestSuitVm>();

      foreach (var aSuit in suits)
      {
        var suit = aSuit.Trim();
        
        if (string.IsNullOrEmpty(suit))
          continue;

        var testSuit = new TestSuitVm(this, suit, config, compilerMessages);
        
        if (selectePath != null)
        {
          if (testSuit.FullPath == selectePath)
            testSuit.IsSelected = true; // Прикольно что по другому фокус не изменить!
          else foreach (var test in testSuit.Tests)
            if (test.FullPath == selectePath)
              test.IsSelected = true;
        }
      }

      TestSuits.CollectionChanged += TestSuits_CollectionChanged;
      IsDirty = false;
    }

    public string Name { get { return Path.GetFileName(SolutinFilePath); } }

    void TestSuits_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
      IsDirty = true;
    }

    public void Save()
    {
      if (!IsDirty)
        return;

      var builder = new StringBuilder();

      foreach (var testSuitVm in TestSuits)
        builder.AppendLine(testSuitVm.Name);

      File.WriteAllText(SolutinFilePath, builder.ToString(), Encoding.UTF8);

      IsDirty = false;
    }

    public override string ToString()
    {
      return Name;
    }

    public string[] GetUnattachedTestSuits()
    {
      var dir = Path.GetDirectoryName(SolutinFilePath);
      return Directory.GetDirectories(dir ?? "").Select(Path.GetFileName).Except(TestSuits.Select(s => s.Name)).ToArray();
    }

    public ITestTreeNode Parent
    {
      get { return null; }
    }
  }
}
