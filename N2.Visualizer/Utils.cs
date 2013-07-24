using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace N2.Visualizer
{
  static class Utils
  {
    public static GrammarDescriptor[] LoadAssembly(string assemblyFilePath)
    {
      var assembly = Assembly.LoadFrom(assemblyFilePath);
      return GrammarDescriptor.GetDescriptors(assembly);
    }

    public static string MakeMakeRelativePath(string baseDir, string filePath)
    {
      var assemblyUri = new Uri(filePath);
      var rootUri = new Uri(EnsureBackslash(baseDir));
      return rootUri.MakeRelativeUri(assemblyUri).ToString();
    }

    private static string EnsureBackslash(string baseDir)
    {
      return baseDir.Length == 0 ? "" : baseDir[baseDir.Length - 1] == '\\' ? baseDir : baseDir + @"\";
    }
  }
}
