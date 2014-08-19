using Nitra.DebugStrategies;
using Nitra.ViewModels;
using Nitra.Visualizer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nitra.TestsLauncher
{
  class Program
  {
    static void Main(string[] args)
    {
      if (args.Length != 2)
      {
        Console.WriteLine("Usage: Nitra.TestsLauncher.exe tests-location-root-path (Debug|Releath)");
        return;
      }

      var testsLocationRoot = args[0];
      var config = args[1];

      if (!Directory.Exists(testsLocationRoot ?? ""))
      {
        Console.WriteLine("The directory '" + testsLocationRoot + "' not exists.");
        return;
      }

      var testSuits = new List<TestSuitVm>();
      Utils.LoadTestSuits(testsLocationRoot, null, config, testSuits);

      var recovery = new RecoveryVisualizer();

      foreach (var suit in testSuits)
      {
        foreach (var test in suit.Tests)
        {
          test.Run(recovery.Strategy);
        }
      }
    }
  }
}
