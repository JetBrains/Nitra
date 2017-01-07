using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NitraCommonIde
{
  public class ProjectSupport
  {
    public ProjectSupport(string caption, string typeFullName, string path)
    {
      Caption      = caption;
      TypeFullName = typeFullName;
      Path         = path;
    }

    public string Caption      { get; }
    public string TypeFullName { get; }
    public string Path         { get; }
  }

  public sealed class Config
  {
    public ProjectSupport ProjectSupport { get; }
    public LanguageInfo[] Languages      { get; }

    /// <summary>Record Constructor</summary>
    /// <param name="projectSupport"><see cref="ProjectSupport"/></param>
    /// <param name="languages"><see cref="Languages"/></param>
    public Config(ProjectSupport projectSupport, LanguageInfo[] languages)
    {
      ProjectSupport = projectSupport;
      Languages      = languages;
    }
  }

  public class LanguageInfo
  {
    public string                   Name       { get; }
    public string                   Path       { get; }
    public ImmutableHashSet<string> Extensions { get; }

    public List<DynamicExtensionInfo> DynamicExtensions = new List<DynamicExtensionInfo>();

    /// <summary>Record Constructor</summary>
    /// <param name="name"><see cref="Name"/></param>
    /// <param name="path"><see cref="Path"/></param>
    public LanguageInfo(string name, string path, ImmutableHashSet<string> extensions)
    {
      Name       = name;
      Path       = path;
      Extensions = extensions;
    }
  }

  public struct DynamicExtensionInfo
  {
    public string Name { get; }
    public string Path { get; }

    /// <summary>Record Constructor</summary>
    /// <param name="name"><see cref="Name"/></param>
    /// <param name="path"><see cref="Path"/></param>
    public DynamicExtensionInfo(string name, string path)
    {
      Name = name;
      Path = path;
    }
  }

  public static class NitraCommonPackage
  {
    internal static List<Config> Configs { get; } = new List<Config>();

    public static void AddProjectType(Config config)
    {
      Configs.Add(config);
    }
  }
}
