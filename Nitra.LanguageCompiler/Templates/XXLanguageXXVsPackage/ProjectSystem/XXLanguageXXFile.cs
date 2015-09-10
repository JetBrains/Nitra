using Nitra.Declarations;
using Nitra.ProjectSystem;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using Nitra;

namespace XXNamespaceXX.ProjectSystem
{
  class XXLanguageXXFile : ConcreteFile, IDisposable
  {
    private readonly IPsiSourceFile _psiSourceFile;
    private readonly XXLanguageXXProject _project;
    private readonly string _fullName;

    public XXLanguageXXFile(FileStatistics statistics, IPsiSourceFile psiSourceFile, XXLanguageXXProject project)
      : base(null)// TODO: add ruleDescriptor
    {
      _psiSourceFile = psiSourceFile;
      _fullName      = psiSourceFile.GetLocation().FullPath;
      _project       = project;
      psiSourceFile.Document.DocumentChanged += Document_DocumentChanged;
    }

    void Document_DocumentChanged(object sender, JetBrains.DataFlow.EventArgs<JetBrains.DocumentModel.DocumentChange> args)
    {
      
    }

    public override SourceSnapshot GetSource()
    {
      return new SourceSnapshot(_psiSourceFile.Document.GetText());// TODO: add path
    }

    public override Project Project
    {
      get { return _project; }
    }

    public override int Length
    {
      get { return _psiSourceFile.Document.GetTextLength(); }
    }

    public override string FullName
    {
      get { return _fullName; }
    }

    public void Dispose()
    {
      _psiSourceFile.Document.DocumentChanged -= Document_DocumentChanged;
    }
  }
}
