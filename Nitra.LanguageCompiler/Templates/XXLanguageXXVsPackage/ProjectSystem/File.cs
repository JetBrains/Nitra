using Nitra;
using Nitra.Declarations;
using Nitra.ProjectSystem;
using Nitra.VisualStudio.Coloring;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

using JetBrains.DataFlow;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;

namespace XXNamespaceXX.ProjectSystem
{
  public class XXLanguageXXFile : ConcreteFile, IDisposable
  {
    private readonly IPsiSourceFile _psiSourceFile;
    private readonly XXLanguageXXProject _project;
    private readonly string _fullName;
    private int _errorCount;

    public XXLanguageXXFile(FileStatistics statistics, IPsiSourceFile psiSourceFile, XXLanguageXXProject project)
      : base(null)// TODO: add statistics
    {
      _psiSourceFile = psiSourceFile;
      _fullName      = psiSourceFile.GetLocation().FullPath;
      _project       = project;
    }

    public override Language Language
    {
      get { return XXLanguageInstanceXX; }
    }

    public override SourceSnapshot GetSource()
    {
      return new SourceSnapshot(_psiSourceFile.Document.GetText(), this);// TODO: add path
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

    public IPsiSourceFile PsiSourceFile
    {
      get { return _psiSourceFile; }
    }

    public IProjectFile ProjectFile
    {
      get { return _psiSourceFile.ToProjectFile(); }
    }

    public IDocument Document
    {
      get { return _psiSourceFile.Document; }
    }

    protected override ParseSession GetParseSession()
    {
      var session = base.GetParseSession();
      session.DynamicExtensions = _project.GrammarDescriptors;
      return session;
    }

    public void Dispose()
    {
    }

    public void OnFileChanged(DocumentChange documentChange)
    {
      ResetCache();
      Project.UpdateProperties();

      var visitor = new CalcSymbolErrorsAstVisitor();
      this.Ast.Accept(visitor);
      var errorCount = visitor.ErrorCount;
      if (_errorCount != errorCount)
        OnRedraw();
      _errorCount = errorCount;
    }
  }
}
