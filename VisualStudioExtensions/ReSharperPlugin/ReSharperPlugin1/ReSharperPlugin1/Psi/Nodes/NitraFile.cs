using System.Collections.Generic;
using System.Text.RegularExpressions;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;

namespace JetBrains.Test
{
  internal class NitraProject
  {
    private readonly Dictionary<string, NitraDeclaredElement>         _declaredElements = new Dictionary<string, NitraDeclaredElement>();
    private readonly Dictionary<IDeclaredElement, List<IDeclaration>> _declarations     = new Dictionary<IDeclaredElement, List<IDeclaration>>();
    private readonly Dictionary<IDeclaredElement, List<NitraNameReference>> _references = new Dictionary<IDeclaredElement, List<NitraNameReference>>();

    public ITreeNode Add(IPsiSourceFile sourceFile, string text, int start, int len)
    {
      var name = text.Substring(start, len);
      NitraDeclaredElement declaredElement;
      if (!_declaredElements.TryGetValue(name.ToLower(), out declaredElement))
        declaredElement = new NitraDeclaredElement(sourceFile.GetSolution(), name);

      if (name.Length > 0 && char.IsUpper(name[0]))
      {
        var node = new NitraDeclaration(declaredElement, sourceFile, name, start, len);
        declaredElement.AddDeclaration(node);
        return node;
      }
      else
      {
        List<NitraNameReference> refs;
        if (!_references.TryGetValue(declaredElement, out refs))
          refs = new List<NitraNameReference>();

        var node = new NitraNameReference(sourceFile, name, start, len);
        refs.Add(node);
        return node;
      }
    }

    public ITreeNode AddWhitespace(IPsiSourceFile sourceFile, string text, int start, int len)
    {
      return new NitraWhitespaceElement(text, start, len);
    }
  }

  class NitraFile : FileElementBase
  {
    private readonly IPsiSourceFile _sourceFile;
    private NitraProject _nitraProject = new NitraProject();

    public NitraFile()
    {
      this.ReferenceProvider = new NitraReferenceProvider();
    }

    public NitraFile(IPsiSourceFile sourceFile, CommonIdentifierIntern commonIdentifierIntern)
    {
      _sourceFile = sourceFile;
      this.ReferenceProvider = new NitraReferenceProvider();

      var regex = new Regex(@"(\w(\w|\d)+)");
      var text = sourceFile.Document.GetText();
      var matchs = regex.Matches(text);
      var prev = 0;
      foreach (Match match in matchs)
      {
        var spaceLen = match.Index - prev;
        if (spaceLen > 0)
          this.AddChild(_nitraProject.AddWhitespace(sourceFile, text, prev, spaceLen));

        this.AddChild(_nitraProject.Add(sourceFile, text, match.Index, match.Length));
        prev = match.Index + match.Length;
      }
    }

    public override NodeType NodeType
    {
      get { return NitraFileNodeType.Instance; }
    }

    public override PsiLanguageType Language
    {
      get { return DslLanguage.Instance; }
    }

    public override ReSharper.Psi.Tree.ITreeNode FirstChild
    {
      get
      {
        return base.FirstChild;
      }
    }

    public override ReferenceCollection GetFirstClassReferences()
    {
      return ReferenceCollection.Empty;
    }
  }
}