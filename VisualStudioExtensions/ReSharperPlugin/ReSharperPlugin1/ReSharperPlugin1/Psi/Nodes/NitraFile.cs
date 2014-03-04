using System.Globalization;
using System.Text.RegularExpressions;
using JetBrains.ReSharper.Feature.Services.Lookup;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;

namespace JetBrains.Test
{
  class NitraFile : FileElementBase, INitraAst
  {
    private readonly IPsiSourceFile _sourceFile;
    private readonly NitraProject _nitraProject = new NitraProject();
    public NitraProject Project { get { return _nitraProject;  }}

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
      var len = this.GetTextLength();

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