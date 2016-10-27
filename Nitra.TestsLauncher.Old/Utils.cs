using System.Collections;
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
using Nitra.ProjectSystem;
using Nitra.Visualizer.Serialization;

namespace Nitra.Visualizer
{
  public static class Utils
  {
    static readonly Regex _configRx = new Regex(@"[\\/](Release|Debug)[\\/]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static Assembly LoadAssembly(string assemblyFilePath, string config)
    {
      assemblyFilePath = UpdatePathForConfig(assemblyFilePath, config);

      var assembly = Assembly.ReflectionOnlyLoadFrom(assemblyFilePath);
      var runtime = typeof(ParseResult).Assembly.GetName();
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

      return assembly;
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
      var result = PathRelativePathTo(builder, @from, isFromDir ? FILE_ATTRIBUTE.DIRECTORY : 0, to, isToDir ? FILE_ATTRIBUTE.DIRECTORY : 0);

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

    public static bool IsInvalidDirName(string testSuiteName)
    {
      var invalidChars = Path.GetInvalidFileNameChars();
      return testSuiteName.Any(invalidChars.Contains);
    }

    public static string MakeXml([NotNull] string root, [NotNull] Language language, [NotNull] IEnumerable<GrammarDescriptor> dynamicExtensions, LibReference[] libs, bool disableSemanticAnalysis = true)
    {
      return SerializationHelper.Serialize(language, dynamicExtensions, libs, path => MakeRelativePath(@from: root, isFromDir: true, to: path, isToDir: false), disableSemanticAnalysis);
    }

    public static bool IsEmpty(this IEnumerable seq)
    {
      var collection = seq as ICollection;

      if (collection != null)
        return collection.Count > 0;

      foreach (var x in seq)
        return true;

      return false;
    }

    public static int Count(this IEnumerable seq)
    {
      var collection = seq as ICollection;

      if (collection != null)
        return collection.Count;

      var count = 0;
      foreach (var x in seq)
      {
        count++;
      }
      return count;
    }

    public static string Escape(string str)
    {
      return str.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    public static string WrapToXaml(string xaml)
    {
      var content = @"
<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
" + xaml + @"
</Span>";

      return content;
    }

    public static string NitraRuntimePath
    {
        get { return new Uri(typeof(Nitra.Location).Assembly.CodeBase).LocalPath; }
    }
  }
}
