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
  internal class NitraNameReference : NitraTokenElement, IReference, INitraAst
  {
    public NitraNameReference(IPsiSourceFile sourceFile, string name, int start, int len) : base(name, start, len)
    {
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
      var name = GetText();
      var file = (NitraFile)this.GetContainingFile();

      if (file == null)
        return ResolveResultWithInfo.Unresolved;

      var declaredElement = file.Project.LookupDeclaredElement(name);
      if (declaredElement == null)
        return ResolveResultWithInfo.Unresolved;
      var resolveResult = ResolveResultFactory.CreateResolveResult(declaredElement);
      return new ResolveResultWithInfo(resolveResult, ResolveErrorType.OK); ;
    }

    public TreeTextRange GetTreeTextRange()
    {
      return new TreeTextRange(new TreeOffset(myCachedOffsetData), GetTextLength());
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

    public override string ToString()
    {
      return "Reference " + myCachedOffsetData + ":" + GetText();
    }
  }
}
