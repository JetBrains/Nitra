using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util.DataStructures;

namespace JetBrains.Test
{
  internal class NitraDeclaredElement : IDeclaredElement, INitraAst
  {
    public ISolution  Solution  { get; private set; }
    public string     ShortName { get; private set; }

    private readonly List<IDeclaration> _declarations = new List<IDeclaration>();

    public NitraDeclaredElement(ISolution solution, string name)
    {
      Solution = solution;
      ShortName = name;
    }

    public NitraDeclaredElement()
    {
    }

    public void AddDeclaration(NitraDeclaration declaration)
    {
      if (_declarations.Contains(declaration))
        Debug.Assert(false);
      _declarations.Add(declaration);
    }

    public IPsiServices GetPsiServices()
    {
      return Solution.GetPsiServices();
    }

    public IList<IDeclaration> GetDeclarations()
    {
      return _declarations;
    }

    public IList<IDeclaration> GetDeclarationsIn(IPsiSourceFile sourceFile)
    {
      return GetDeclarations().Where(declaration => declaration.GetSourceFile() == sourceFile).ToList();
    }

    public DeclaredElementType GetElementType()
    {
      return NitraDeclaredElementType.Instance;
    }

    public XmlNode GetXMLDoc(bool inherit)
    {
      return null;
    }

    public XmlNode GetXMLDescriptionSummary(bool inherit)
    {
      return null;
    }

    public bool IsValid()
    {
      return _declarations.Count > 0;
    }

    public bool IsSynthetic()
    {
      return false;
    }

    public HybridCollection<IPsiSourceFile> GetSourceFiles()
    {
      return new HybridCollection<IPsiSourceFile>(GetDeclarations().Select(declaration => declaration.GetSourceFile()).Distinct().ToList());
    }

    public bool HasDeclarationsIn(IPsiSourceFile sourceFile)
    {
      return GetSourceFiles().Contains(sourceFile);
    }

    public bool CaseSensistiveName { get { return false; } }

    public PsiLanguageType PresentationLanguage { get { return DslLanguage.Instance; } }
  }
}
