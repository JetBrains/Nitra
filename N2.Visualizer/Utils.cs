using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Xml.Linq;
using N2.Visualizer.Annotations;

namespace N2.Visualizer
{
  static class Utils
  {
    public static GrammarDescriptor[] LoadAssembly(string assemblyFilePath)
    {
      var assembly = Assembly.LoadFrom(assemblyFilePath);
      return GrammarDescriptor.GetDescriptors(assembly);
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
