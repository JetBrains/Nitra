using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Application;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Test;
using JetBrains.Util;

namespace ReSharperPlugin1.Psi.References
{
  public class NitraReferenceFactory  : IReferenceFactory
  {
    [ReferenceProviderFactory]
    public class Factory : IReferenceProviderFactory
    {
      private readonly IShellLocks myShellLocks;

      public Factory(IShellLocks shellLocks)
      {
        myShellLocks = shellLocks;
      }

      public IReferenceFactory CreateFactory(IPsiSourceFile sourceFile, IFile file)
      {
        myShellLocks.AssertReadAccessAllowed();
        return new NitraReferenceFactory();
      }

      public event Action OnChanged;
    }

    private IEnumerable<IReference> GetReferencesImpl(ITreeNode element)
    {
      if (!(element is INitraAst))
        yield break;

      var reference = element as IReference;

      if (reference != null)
        yield return reference;

      if (element is ITokenNode)
        yield break;

      foreach (var n in element.Children())
      {
        if (ReferenceEquals(n, element))
          continue;

        foreach (var reference1 in GetReferencesImpl(n))
          yield return reference1;
      }
    }

    public IReference[] GetReferences(ITreeNode element, IReference[] oldReferences)
    {
      var references = GetReferencesImpl(element).ToArray();
      return references;
    }

    public bool HasReference(ITreeNode element, IReferenceNameContainer names)
    {
      return false;
    }

    public bool HasReference(ITreeNode element, ICollection<string> names)
    {
      return GetReferencesImpl(element).Any();
    }
  }
}
