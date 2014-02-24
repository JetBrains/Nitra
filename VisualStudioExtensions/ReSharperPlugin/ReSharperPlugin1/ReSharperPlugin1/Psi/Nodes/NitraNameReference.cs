using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace JetBrains.Test
{
  internal class NitraNameDeclaration : NitraTokenElement
  {
    public NitraNameDeclaration(IPsiSourceFile sourceFile, string name, int start, int len) : base(name, start, len)
    {
      Debug.Assert(myCachedOffsetData == start);
      Debug.Assert(GetText() == name);
      Debug.Assert(GetTextLength() == len);
    }

    public override NodeType NodeType
    {
      get { return NitraIdentifierNodeType.Instance; }
    }
  }

  internal class NitraNameReference : NitraCompositeElement, IReference
  {
    public NitraNameReference(IPsiSourceFile sourceFile, string name, int start, int len)
    {
      myCachedOffsetData = start;
    }

    public override NodeType NodeType
    {
      get { return NitraIdentifierNodeType.Instance; }
    }

    public override ReferenceCollection GetFirstClassReferences()
    {
      return new ReferenceCollection(this);
    }

    public void PutData<T>(Key<T> key, T val) where T : class
    {
    }

    public T GetData<T>(Key<T> key) where T : class
    {
      throw new System.NotImplementedException();
    }

    public IEnumerable<KeyValuePair<object, object>> EnumerateData()
    {
      throw new System.NotImplementedException();
    }

    public ITreeNode GetTreeNode()
    {
      return this;
    }

    public string GetName()
    {
      return GetText();
    }

    public IEnumerable<string> GetAllNames()
    {
      yield return GetText();
    }

    public ISymbolTable GetReferenceSymbolTable(bool useReferenceName)
    {
      return EmptySymbolTable.INSTANCE;
    }

    public ResolveResultWithInfo Resolve()
    {
      throw new System.NotImplementedException();
    }

    public TreeTextRange GetTreeTextRange()
    {
      return new TreeTextRange(myCachedOffsetData);
    }

    public IReference BindTo(IDeclaredElement element)
    {
      throw new System.NotImplementedException();
    }

    public IReference BindTo(IDeclaredElement element, ISubstitution substitution)
    {
      return BindTo(element);
    }

    public IAccessContext GetAccessContext()
    {
      return null;
    }

    public bool HasMultipleNames
    {
      get { return false; }
    }

    public ResolveResultWithInfo CurrentResolveResult { get; set; }
  }
}