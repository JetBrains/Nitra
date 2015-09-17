using Nitra.Declarations;
using Nitra.ProjectSystem;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;

namespace XXNamespaceXX.ProjectSystem
{
  public class XXLanguageXXProject : Project, IDisposable
  {
    private readonly IProject _project;
    private readonly Dictionary<IProjectFile, XXLanguageXXFile> _filesMap     = new Dictionary<IProjectFile, XXLanguageXXFile>();
    private readonly Dictionary<string,       XXLanguageXXFile> _filePathsMap = new Dictionary<string,       XXLanguageXXFile>(StringComparer.OrdinalIgnoreCase);

    public XXLanguageXXProject(IProject project)
    {
      _project = project;
    }

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

    internal XXLanguageXXFile TryGetFile(IProjectFile file)
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

    public void Dispose()
    {
      foreach (var file in _filesMap.Values)
        file.Dispose();
      _filesMap.Clear();
      _filePathsMap.Clear();
    }
  }
}
