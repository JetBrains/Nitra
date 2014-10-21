using System.Collections.Generic;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Psi;
using JetBrains.Test;
using JetBrains.Util;
using DataConstants = JetBrains.ReSharper.Psi.Services.DataConstants;

namespace JetBrains.Nitra.FindUsages
{
  internal class NitraContextSearchImpl
  {
    public IList<IDeclaredElement> GetCandidates(IDataContext context)
    {
      var reference = context.GetData(DataConstants.REFERENCE);
      var rer = reference as INitraAst;
      if (rer != null)
        return new IDeclaredElement[] { reference.Resolve().Result.DeclaredElement };

      var declaredElements = context.GetData(DataConstants.DECLARED_ELEMENTS);

      if (declaredElements != null)
        return declaredElements.ToArray();

      return EmptyList<IDeclaredElement>.InstanceList;
    }

    public bool IsContextApplicable(IDataContext context)
    {
      var reference = context.GetData(DataConstants.REFERENCE);
      if (reference == null)
      {
        var declaredElements = context.GetData(DataConstants.DECLARED_ELEMENTS);

        if (declaredElements != null)
          foreach (var element in declaredElements)
            if (element is INitraAst)
              return true;

        return false;
      }
      var rer = reference as INitraAst;
      if (rer != null)
      {
        return true;
      }
      return false;
    }
  }
}
