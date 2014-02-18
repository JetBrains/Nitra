using System.Text.RegularExpressions;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;

namespace JetBrains.Test
{
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

      var regex = new Regex(@"(\w(\w|\d))+");
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