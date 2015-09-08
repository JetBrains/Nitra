using Nitra.Declarations;
using Nitra.ProjectSystem;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;

namespace XXNamespaceXX.ProjectSystem
{
  public class XXLanguageXXProject : Project, IDisposable
  {
    private readonly IProject _project;
    private readonly Dictionary<IProjectFile, XXLanguageXXFile> _projectsMap = new Dictionary<IProjectFile, XXLanguageXXFile>();

    public XXLanguageXXProject(IProject project)
    {
      _project = project;
    }

    public override IEnumerable<File> Files { get { return _projectsMap.Values; } }

    public void TryRemoveFile(IProjectFile file)
    {
      XXLanguageXXFile result;
      if (_projectsMap.TryGetValue(file, out result))
      {
        result.Dispose();
        _projectsMap.Remove(file);
      }
    }

    public void TryAddFile(IProjectFile file)
    {
      XXLanguageXXFile nitraFile;
      if (!_projectsMap.TryGetValue(file, out nitraFile))
      {
        var sourceFile = file.ToSourceFile();
        _projectsMap.Add(file, new XXLanguageXXFile(null /*TODO: add statistics*/, sourceFile, this));
      }
    }

    public void Dispose()
    {
      foreach (var file in _projectsMap.Values)
        file.Dispose();
      _projectsMap.Clear();
    }
  }
}
