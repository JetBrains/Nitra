using Nitra.Declarations;
using Nitra.ProjectSystem;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.Util;

namespace XXNamespaceXX.ProjectSystem
{
  public class XXLanguageXXProject : Project, IDisposable
  {
    private readonly IProject _project;
    private readonly Dictionary<IProjectFile, XXLanguageXXFile> _filesMap     = new Dictionary<IProjectFile, XXLanguageXXFile>();
    private readonly Dictionary<string,       XXLanguageXXFile> _filePathsMap = new Dictionary<string,       XXLanguageXXFile>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<GrammarDescriptor> _nitraAssemblies = new HashSet<GrammarDescriptor>();
    internal readonly List<LibReference> _libs = new List<LibReference>();

    public XXLanguageXXProject(XXLanguageXXSolution solution, IProject project)
    {
      _project    = project;
      Solution    = solution;
      Libs        = _libs;
      ProjectDir  = project.ProjectLocationLive.Value.FileAccessPath;
    }

    public IEnumerable<GrammarDescriptor> GrammarDescriptors { get { return _nitraAssemblies; }}

    public override IEnumerable<File> Files { get { return _filesMap.Values; } }

    public void TryRemoveFile(IProjectFile file)
    {
      XXLanguageXXFile result;
      if (_filesMap.TryGetValue(file, out result))
      {
        _filePathsMap.Remove(result.FullName);
        result.Dispose();
        _filesMap.Remove(file);
      }
    }

    internal XXLanguageXXFile TryAddFile(IProjectFile file)
    {
      XXLanguageXXFile nitraFile;
      if (!_filesMap.TryGetValue(file, out nitraFile))
      {
        var sourceFile = file.ToSourceFile();
        nitraFile = new XXLanguageXXFile(null /*TODO: add statistics*/, sourceFile, this);
        _filesMap.Add(file, nitraFile);
        _filePathsMap.Add(nitraFile.FullName, nitraFile);
      }

      return nitraFile;
    }

    [CanBeNull]
    internal XXLanguageXXFile TryGetFile([NotNull] IProjectFile file)
    {
      XXLanguageXXFile nitraFile;
      _filesMap.TryGetValue(file, out nitraFile);
      return nitraFile;
    }

    [CanBeNull]
    internal XXLanguageXXFile TryGetFile([NotNull] string filePath)
    {
      if (filePath == null)
        throw new ArgumentNullException("filePath");

      XXLanguageXXFile nitraFile;
      _filePathsMap.TryGetValue(filePath, out nitraFile);
      return nitraFile;
    }

    static GrammarDescriptor[] LoadAssembly(string assemblyFilePath)
    {
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
      return GrammarDescriptor.GetDescriptors(assembly);
    }

    internal void TryAddNitraExtensionAssemblyReference(FileSystemPath path)
    {
      var pathString = path.FileAccessPath;
      var grammagDescriptors = LoadAssembly(pathString);
      _nitraAssemblies.AddRange(grammagDescriptors);
    }

    public void Dispose()
    {
      foreach (var file in _filesMap.Values)
        file.Dispose();
      _filesMap.Clear();
      _filePathsMap.Clear();
      _libs.Clear();
    }
  } // class
}  // namespace
