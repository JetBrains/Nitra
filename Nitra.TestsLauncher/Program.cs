using Nitra.DebugStrategies;
using Nitra.ViewModels;
using Nitra.Visualizer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

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
              
      
      var stackSize = 20 * 1024 * (IntPtr.Size == 8 ? 8 : 1) * 1024;
      var thread = new Thread(() => Start(testsLocationRoot, config), stackSize);
      thread.Name = "Main test thread";
      thread.Start();
      thread.Join();
    }

    const string IndentString = "    ";
    static string _currentIndent = "";

    static void Indent()
    {
      _currentIndent += IndentString;
    }

    static void Unindent()
    {
      _currentIndent = _currentIndent.Substring(0, _currentIndent.Length - IndentString.Length);
    }

    static void PrintLine(string text, ConsoleColor color)
    {
      Console.ForegroundColor = color;
      PrintLine(text);
      Console.ResetColor();
    }

    static void Print(string text, ConsoleColor color)
    {
      Console.ForegroundColor = color;
      Print(text);
      Console.ResetColor();
    }

    static void ContinuePrint(string text, ConsoleColor color)
    {
      Console.ForegroundColor = color;
      ContinuePrint(text);
      Console.ResetColor();
    }

    static void PrintLine(string text)
    {
      if (!string.IsNullOrWhiteSpace(text))
        Console.WriteLine(_currentIndent + text.Replace("\n", "\n" + _currentIndent));
    }

    static void Print(string text)
    {
      if (!string.IsNullOrWhiteSpace(text))
        Console.Write(_currentIndent + text.Replace("\n", "\n" + _currentIndent));
    }

    static void ContinuePrint(string text)
    {
      if (!string.IsNullOrWhiteSpace(text))
        Console.WriteLine(text.Replace("\n", "\n" + _currentIndent));
    }

    static void Start(string testsLocationRoot, string config)
    {
      var testSuits = new List<TestSuitVm>();
      Utils.LoadTestSuits(testsLocationRoot, null, config, testSuits);

      var recovery = new RecoveryVisualizer();
      var lastSuit = "";
      var lastTest = "";
      var maxNameLen = CalcMaxNameLen(testSuits);
      var someTestsFailed = false;
      var someTestSuitsFailedToLoad = false;

      foreach (var suit in testSuits)
      {
        lastSuit = suit.Name;
        PrintLine("Test suit: " + suit.Name);
        Indent();

        if (suit.TestState == TestState.Ignored)
        {
          PrintLine(suit.Hint, ConsoleColor.Red);
          someTestSuitsFailedToLoad = true;
          Unindent();
          continue;
        }

        foreach (var test in suit.Tests)
        {
          lastTest = test.Name;
          var dots = maxNameLen - test.Name.Length;
          Print(test.Name + " " + new string('.', dots) + " ");
          Console.Out.Flush();
          test.Run(recovery.Strategy);

          switch (test.TestState)
          {
            case TestState.Skipped:
              ContinuePrint("skipped.", ConsoleColor.Yellow);
              break;
            case TestState.Failure:
              ContinuePrint("failed!", ConsoleColor.Red);
              someTestsFailed = true;
             break;
            case TestState.Ignored:
              ContinuePrint("ignored.", ConsoleColor.Yellow);
              break;
            case TestState.Inconclusive:
              ContinuePrint("inconclusive.", ConsoleColor.Yellow);
              break;
            case TestState.Success:
              ContinuePrint("passed.", ConsoleColor.Green);
              break;
            default:
              break;
          }
        }

        Unindent();
      }

      if (someTestSuitsFailedToLoad)
        PrintLine("Some test suits is failed to load!", ConsoleColor.Red);
      if (someTestsFailed)
        PrintLine("Some tests is failed!", ConsoleColor.Red);

      Console.WriteLine("done...");
      //Console.ReadLine();
    }

    private static int CalcMaxNameLen(List<TestSuitVm> testSuits)
    {
      int maxNameLen = 0;

      foreach (var suit in testSuits)
        foreach (var test in suit.Tests)
          if (test.Name.Length > maxNameLen)
            maxNameLen = test.Name.Length;
      maxNameLen += 3;
      return maxNameLen;
    }
  }
}
