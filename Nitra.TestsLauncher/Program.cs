using Nitra.ViewModels;
using Nitra.Visualizer;

using Nemerle.Diff;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Nitra.ProjectSystem;
using File = System.IO.File;

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

      var solutinFilePath = args[0];
      var config = args[1];

      if (!File.Exists(solutinFilePath ?? ""))
      {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("The solution '" + solutinFilePath + "' does not exist.");
        Console.ResetColor();
        return;
      }


      var stackSize = 20 * 1024 * (IntPtr.Size == 8 ? 8 : 1) * 1024;
      var thread = new Thread(() => Start(solutinFilePath, config), stackSize);
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

    static void Start(string solutinFilePath, string config)
    {
      var solution = new SolutionVm(solutinFilePath, null, config);
      var testSuites = solution.TestSuites;

      var maxNameLen = CalcMaxNameLen(testSuites);
      var someTestsFailed = false;
      var someTestSuitesFailedToLoad = false;

      foreach (var suite in testSuites)
      {
        PrintLine("Test suite: " + suite.Name);
        Indent();

        if (suite.TestState == TestState.Ignored)
        {
          PrintLine(suite.Hint, ConsoleColor.Red);
          someTestSuitesFailedToLoad = true;
          Unindent();
          continue;
        }

        foreach (var test in suite.Tests)
        {
          var dots = maxNameLen - test.Name.Length;
          Print(test.Name + " " + new string('.', dots) + " ");
          Console.Out.Flush();
          {
            var testFile = test as TestVm;
            if (testFile != null)
              someTestsFailed |= RunTestFile(testFile);
          }
          {
            var testFolder = test as TestFolderVm;
            if (testFolder != null)
              foreach (var testFile in testFolder.Tests)
                someTestsFailed |= RunTestFile(testFile);
          }
        }

        Unindent();
      }

      if (someTestSuitesFailedToLoad)
        PrintLine("Some test suits is failed to load!", ConsoleColor.Red);
      if (someTestsFailed)
        PrintLine("Some tests is failed!", ConsoleColor.Red);

      Console.WriteLine("done...");

      if (someTestsFailed || someTestSuitesFailedToLoad)
      {
        //Console.ReadLine();
        Environment.Exit(-1);
      }
    }

    private static bool RunTestFile(TestVm testFile)
    {
      testFile.Run(RecoveryAlgorithm.Smart);

      switch (testFile.TestState)
      {
        case TestState.Skipped:
          ContinuePrint("skipped.", ConsoleColor.Yellow);
          break;
        case TestState.Failure:
          ContinuePrint("failed!", ConsoleColor.Red);
          Indent();
          Diff(testFile);
          Unindent();

          return true;

        case TestState.Ignored:
          ContinuePrint("ignored.", ConsoleColor.Yellow);
          break;
        case TestState.Inconclusive:
          ContinuePrint("inconclusive.", ConsoleColor.Yellow);
          break;
        case TestState.Success:
          ContinuePrint("passed.", ConsoleColor.Green);
          break;
      }

      return false;
    }

    private static void Diff(TestVm test)
    {
      var textA = Split(test.Gold);
      var textB = Split(test.PrettyPrintResult);
      const int rangeToShow = 3;
      var indexA = 0;
      var output = new List<string>();

      foreach (var diffItem in textA.Diff(textB))
      {
        //в начале итерации indexA содержит индекс строки идущей сразу за предыдущим блоком

        // определяем нужно ли выводить разделитель
        if (diffItem.Index - indexA > rangeToShow * 2)
        {
          //показываем строки идущие после предыдущего блока
          for (var i = 0; i < rangeToShow; ++i)
          {
            PrintLine("  " + textA[indexA]);
            ++indexA;
          }

          PrintLine("  " + "...", ConsoleColor.Gray);

          //показываем строки идущие перед текущим блоком
          indexA = diffItem.Index - rangeToShow;
          for (var i = 0; i < rangeToShow; ++i)
          {
            PrintLine("  " + textA[indexA]);
            ++indexA;
          }
        }
        else
        {
          //показываем строки между блоками
          while (indexA < diffItem.Index)
          {
            PrintLine("  " + textA[indexA]);
            ++indexA;
          }
        }

        // показываем удаленные строки
        for (var i = 0; i < diffItem.Deleted; ++i)
        {
          PrintLine("- " + textA[indexA], ConsoleColor.Red);
          ++indexA;
        }

        // показываем добавленные строки
        foreach (var insertedItem in diffItem.Inserted)
          PrintLine("+ " + insertedItem, ConsoleColor.Green);
      }

      // показываем не более rangeToShow последующих строк
      var tailLinesToShow = Math.Min(rangeToShow, textA.Length - indexA);

      for (var i = 0; i < tailLinesToShow; ++i)
      {
        PrintLine("  " + textA[indexA]);
        ++indexA;
      }

      if (indexA < textA.Length)
        PrintLine("  " + "...", ConsoleColor.Gray);

      PrintLine("END-DIFF", ConsoleColor.Gray);
    }

    private static string[] Split(string gold)
    {
      return gold.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
    }

    private static int CalcMaxNameLen(IEnumerable<TestSuiteVm> testSuites)
    {
      int maxNameLen = 0;

      foreach (var suite in testSuites)
        foreach (var test in suite.Tests)
          if (test.Name.Length > maxNameLen)
            maxNameLen = test.Name.Length;
      maxNameLen += 3;
      return maxNameLen;
    }
  }
}
