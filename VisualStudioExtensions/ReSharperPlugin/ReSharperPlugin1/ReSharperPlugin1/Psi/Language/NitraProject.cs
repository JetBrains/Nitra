using System.Collections.Generic;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Tree;

namespace JetBrains.Test
{
  internal class NitraProject
  {
    private readonly Dictionary<string, NitraDeclaredElement>         _declaredElements = new Dictionary<string, NitraDeclaredElement>();
    private readonly Dictionary<IDeclaredElement, List<NitraNameReference>> _references = new Dictionary<IDeclaredElement, List<NitraNameReference>>();

    public NitraDeclaredElement LookupDeclaredElement(string name)
    {
      NitraDeclaredElement result;
      if (!_declaredElements.TryGetValue(name.ToUpperInvariant(), out result))
        return null;

      return result;
    }

    public List<NitraNameReference> LookupReferences(string name)
    {
      return LookupReferences(LookupDeclaredElement(name));
    }

    public List<NitraNameReference> LookupReferences(NitraDeclaredElement declaredElement)
    {
      if (declaredElement == null)
        return new List<NitraNameReference>();

      List<NitraNameReference> results;
      if (!_references.TryGetValue(declaredElement, out results))
        return new List<NitraNameReference>();

      return results;
    }

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
        _declaredElements.Add(name, declaredElement);
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
      return new NitraWhitespaceElement(text.Substring(start, len), start, len);
    }
  }
}