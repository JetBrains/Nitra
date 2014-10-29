using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace Nitra.ViewModels
{
  public class SolutionVm
  {
    public ObservableCollection<TestSuitVm> TestSuits { get; private set; }

    public SolutionVm(string solutinFilePath, string selectePath, string config)
    {
      var suits   = File.ReadAllLines(solutinFilePath);
      var rootDir = Path.GetDirectoryName(solutinFilePath);
      Debug.Assert(rootDir != null, "rootDir != null");
      TestSuits   = new ObservableCollection<TestSuitVm>();

      foreach (var aSuit in suits)
      {
        var suit = aSuit.Trim();
        
        if (string.IsNullOrEmpty(suit))
          continue;
        
        var dir = Path.Combine(rootDir, suit);
        var testSuit = new TestSuitVm(rootDir, dir, config);
        
        if (selectePath != null)
        {
          if (testSuit.FullPath == selectePath)
            testSuit.IsSelected = true; // Прикольно что по другому фокус не изменить!
          else foreach (var test in testSuit.Tests)
            if (test.FullPath == selectePath)
              test.IsSelected = true;
        }
        
        TestSuits.Add(testSuit);
      }
    }

    private void Add()
    {
    }
  }
}
