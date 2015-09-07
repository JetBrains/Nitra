﻿using Nitra.Declarations;
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
  class XXLanguageXFile : ConcreteFile, IDisposable
  {
    private readonly IPsiSourceFile _psiSourceFile;
    private readonly XXLanguageXProject _project;

    public XXLanguageXFile(FileStatistics statistics, IPsiSourceFile psiSourceFile, XXLanguageXProject project)
      : base(null)// TODO: add ruleDescriptor
    {
      _psiSourceFile = psiSourceFile;
      _project = project;
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
      get { return _psiSourceFile.GetLocation().FullPath; }
    }

    public void Dispose()
    {
      _psiSourceFile.Document.DocumentChanged -= Document_DocumentChanged;
    }
  }
}
