using System.Collections.Generic;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;

namespace JetBrains.Test
{
  internal class NitraReferenceProvider : IReferenceProvider
  {
    public ReferenceCollection GetReferences(ITreeNode element, ICollection<string> names)
    {
      return ReferenceCollection.Empty;
    }

    public ReferenceCollection GetReferences(ITreeNode element, IReferenceNameContainer names)
    {
      return ReferenceCollection.Empty;
    }

    public bool ContainsReference(ITreeNode element, IReference reference)
    {
      return true;
    }
  }
}
