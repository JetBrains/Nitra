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
  public class XXLanguageXProject : Project
  {
    private readonly IProject _project;
    private readonly Dictionary<IPsiSourceFile, XXLanguageXFile> _projectsMap = new Dictionary<IPsiSourceFile, XXLanguageXFile>();

    public XXLanguageXProject(IProject project)
    {
      _project = project;
    }

    public override IEnumerable<File> Files { get { return _projectsMap.Values; } }

    public void TryRemoveFile(IProjectFile file)
    {
      foreach (var psiSourceFile in file.ToSourceFiles())
      {
        XXLanguageXFile result;
        if (_projectsMap.TryGetValue(psiSourceFile, out result))
          _projectsMap.Remove(psiSourceFile);
      }
    }

    public void TryAddFile(IProjectFile file)
    {
      foreach (var psiSourceFile in file.ToSourceFiles())
      {
        XXLanguageXFile nitraFile;
        if (!_projectsMap.TryGetValue(psiSourceFile, out nitraFile))
          _projectsMap.Add(psiSourceFile, new XXLanguageXFile(null, psiSourceFile));
      }
    }
  }
}
