using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using N2.Visualizer.Annotations;
using N2.Visualizer.Properties;

namespace N2.Visualizer
{
  static class Utils
  {
    static readonly Regex _configRx = new Regex(@"[\\/](Release|Debug)[\\/]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static GrammarDescriptor[] LoadAssembly(string assemblyFilePath)
    {
      assemblyFilePath = UpdatePathForConfig(assemblyFilePath);

      var assembly = Assembly.LoadFrom(assemblyFilePath);
      var runtime = typeof(N2.Internal.Parser).Assembly.GetName();
      foreach (var reference in assembly.GetReferencedAssemblies())
      {
        if (reference.Name == runtime.Name)
        {
          if (reference.Version == runtime.Version)
            break;
          throw new ApplicationException("Assembly '" + assemblyFilePath + "' use incompatible runtime (N2.Runtime.dll) version " + reference.Version
            + ". The current runtime has version " + runtime.Version + ".");
        }
      }
      return GrammarDescriptor.GetDescriptors(assembly);
    }

    public static string UpdatePathForConfig(string assemblyFilePath)
    {
      return _configRx.Replace(assemblyFilePath, @"\" + Settings.Default.Config + @"\");
    }

    public static string MakeRelativePath(string baseDir, string filePath)
    {
      var assemblyUri = new Uri(filePath);
      var rootUri = new Uri(EnsureBackslash(baseDir));
      return rootUri.MakeRelativeUri(assemblyUri).ToString();
    }

    private static string EnsureBackslash(string baseDir)
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
      var libs = syntaxModules.GroupBy(m => MakeRelativePath(root, m.GetType().Assembly.Location))
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
  }
}
