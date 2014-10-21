using Nitra.ViewModels;
using Nitra.Visualizer.Annotations;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Nitra.Visualizer
{
  public static class Utils
  {
    static readonly Regex _configRx = new Regex(@"[\\/](Release|Debug)[\\/]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static GrammarDescriptor[] LoadAssembly(string assemblyFilePath, string config)
    {
      assemblyFilePath = UpdatePathForConfig(assemblyFilePath, config);

      var assembly = Assembly.ReflectionOnlyLoadFrom(assemblyFilePath);
      var runtime = typeof(Nitra.ParseResult).Assembly.GetName();
      foreach (var reference in assembly.GetReferencedAssemblies())
      {
        if (reference.Name == runtime.Name)
        {
          if (reference.Version == runtime.Version)
            break;
          throw new ApplicationException("Assembly '" + assemblyFilePath + "' use incompatible runtime (Nitra.Runtime.dll) version " + reference.Version
            + ". The current runtime has version " + runtime.Version + ".");
        }
      }
      assembly = Assembly.LoadFrom(assemblyFilePath);
      return GrammarDescriptor.GetDescriptors(assembly);
    }

    public static string UpdatePathForConfig(string assemblyFilePath, string config)
    {
      return _configRx.Replace(assemblyFilePath, @"\" + config + @"\");
    }

    enum FILE_ATTRIBUTE
    {
      DIRECTORY = 0x10,
      NORMAL = 0x80
    }


    [DllImport("shlwapi.dll", EntryPoint = "PathRelativePathTo")]
    static extern bool PathRelativePathTo(StringBuilder lpszDst,
        string from, FILE_ATTRIBUTE attrFrom,
        string to,   FILE_ATTRIBUTE attrTo);

    public static string MakeRelativePath(string from, bool isFromDir, string to, bool isToDir)
    {
      var builder = new StringBuilder(1024);
      var result = PathRelativePathTo(builder, from, isFromDir ? FILE_ATTRIBUTE.DIRECTORY : 0, to, isToDir ? FILE_ATTRIBUTE.DIRECTORY : 0);

      if (result)
        return builder.ToString();

      return to;
    }

    public static string EnsureBackslash(string baseDir)
    {
      return baseDir.Length == 0 ? "" : baseDir[baseDir.Length - 1] == '\\' ? baseDir : baseDir + @"\";
    }

    public static string[] GetAssemblyPaths(string assemblyPaths)
    {
      return assemblyPaths.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    }

    public static bool IsInvalidDirName(string testSuitName)
    {
      var invalidChars = Path.GetInvalidFileNameChars();
      return testSuitName.Any(invalidChars.Contains);
    }

    public static XElement MakeXml([NotNull] string root, [NotNull] IEnumerable<GrammarDescriptor> syntaxModules, [NotNull] RuleDescriptor startRule)
    {
      //  <Config>
      //    <Lib Path="../sss/Json.Grammar.dll"><SyntaxModule Name="JasonParser" /></Lib>
      //    <Lib Path="../sss/JsonEx.dll"><SyntaxModule Name="JsonEx" StartRule="Start" /></Lib>
      //  </Config>
      var libs = syntaxModules.GroupBy(m => MakeRelativePath(from:root, isFromDir:true, to:m.GetType().Assembly.Location, isToDir:false))
        .Select(asm =>
          new XElement("Lib",
            new XAttribute("Path", asm.Key),
            asm.Select(mod =>
              new XElement("SyntaxModule",
                new XAttribute("Name", mod.FullName),
                mod.Rules.Contains(startRule) ? new XAttribute("StartRule", startRule.Name) : null))
            ));

      return new XElement("Config", libs);
    }

    public static void LoadTestSuits(string testsLocationRoot, string path, string config, ICollection<TestSuitVm> testSuits)
    {
      foreach (var dir in Directory.GetDirectories(testsLocationRoot ?? ""))
      {
        var testSuit = new TestSuitVm(testsLocationRoot, dir, config);
        if (path != null)
        {
          if (testSuit.FullPath == path)
            testSuit.IsSelected = true; // Прикольно что по другому фокус не изменить!
          else foreach (var test in testSuit.Tests)
              if (test.FullPath == path)
                test.IsSelected = true;
        }
        testSuits.Add(testSuit);
      }
    }
  }
}
