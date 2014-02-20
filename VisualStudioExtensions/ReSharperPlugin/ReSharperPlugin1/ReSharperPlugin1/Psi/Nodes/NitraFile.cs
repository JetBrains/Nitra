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
    private readonly Dictionary<IDeclaredElement, List<IReference>>   _references       = new Dictionary<IDeclaredElement, List<IReference>>();

    private void AddDecl(IPsiSourceFile sourceFile, string text, int start, int len)
    {
      var name = text.Substring(start, len);
      NitraDeclaredElement declaredElement;
      if (!_declaredElements.TryGetValue(name, out declaredElement))
        declaredElement = new NitraDeclaredElement(sourceFile.GetSolution(), name);

      if (char.IsUpper(name[0]))
        declaredElement.AddDeclaration(new NitraDeclaration());
       var declarations = declaredElement.GetDeclarations();
    }

    private void AddRef(TreeElement node)
    {
      List<TreeElement> refs;

      if (_refs.TryGetValue(node.GetText(), out refs))
        refs = new List<TreeElement>();

      foreach (var element in refs)
      {
        if (node == element)
          return;
      }

      refs.Add(node);
    }

  }

  class NitraFile : FileElementBase
  {
    private readonly IPsiSourceFile _sourceFile;

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
      foreach (Match match in matchs)
      {
        var node = new NitraTokenElement(match.Value);
        this.AddChild(node);
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