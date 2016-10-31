using Nitra.ProjectSystem;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace Nitra.Visualizer.Serialization
{
  public static class SerializationHelper
  {
    private static XmlSerializer _serializer = new XmlSerializer(typeof(Language));

    public static string Serialize(Nitra.Language language, IEnumerable<GrammarDescriptor> dynamicExtensions, LibReference[] libs, Func<string, string> pathConverter, bool disableSemanticAnalysis = false)
    {
      var writer = new StringWriter();
      var data = new Language
      {
        Name = language.FullName,
        Path = pathConverter(language.GetType().Assembly.Location),
        DynamicExtensions = dynamicExtensions.Select(g => new DynamicExtension { Name = g.FullName, Path = pathConverter(g.GetType().Assembly.Location) }).ToArray(),
        Libs = libs.Select(x =>  x.Serialize()).ToArray(),
        DisableSemanticAnalysis = disableSemanticAnalysis
      };
      _serializer.Serialize(writer, data);
      return writer.ToString();
    }

    public static Tuple<Nitra.Language, GrammarDescriptor[], LibReference[], bool> Deserialize(string text, Func<string, Assembly> assemblyResolver)
    {
      var reader = new StringReader(text);
      var languageInfo = (Language)_serializer.Deserialize(reader);

      var languageAssembly = assemblyResolver(languageInfo.Path);
      var language = Nitra.Language.GetLanguages(languageAssembly).FirstOrDefault(l => String.Equals(l.FullName, languageInfo.Name, StringComparison.Ordinal));
      if (language == null)
        throw new ApplicationException(string.Format("Language '{0}' not found in assembly '{1}'.", languageInfo.Name, languageAssembly.Location));

      var dynamicExtensions = new List<GrammarDescriptor>();
      foreach (var extensionInfo in languageInfo.DynamicExtensions)
      {
        var extensionAssembly = assemblyResolver(extensionInfo.Path);
        var descriptor = GrammarDescriptor.GetDescriptors(extensionAssembly).FirstOrDefault(g => String.Equals(g.FullName, extensionInfo.Name, StringComparison.Ordinal));
        if (descriptor == null)
          throw new ApplicationException(string.Format("Syntax module '{0}' not found in assembly '{1}'.", extensionInfo.Name, extensionAssembly.Location));
        dynamicExtensions.Add(descriptor);
      }

      var libs = languageInfo.Libs == null 
        ? new LibReference[0]
        : languageInfo.Libs.Select(LibReference.Deserialize).ToArray();

      var disableSemanticAnalysis = languageInfo.DisableSemanticAnalysis;

      return Tuple.Create(language, dynamicExtensions.ToArray(), libs, disableSemanticAnalysis);
    }
  }

  public sealed class Language
  {
    [XmlAttribute]
    public string Name { get; set; }

    [XmlAttribute]
    public string Path { get; set; }

    public DynamicExtension[] DynamicExtensions { get; set; }

    public string[] Libs { get; set; }

    [XmlAttribute]
    public bool DisableSemanticAnalysis { get; set; }
  }

  public sealed class DynamicExtension
  {
    [XmlAttribute]
    public string Name { get; set; }

    [XmlAttribute]
    public string Path { get; set; }
  }
}
