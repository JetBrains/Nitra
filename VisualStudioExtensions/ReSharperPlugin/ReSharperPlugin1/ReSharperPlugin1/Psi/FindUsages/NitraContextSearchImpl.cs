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
      {
        return new IDeclaredElement[] { reference.Resolve().Result.DeclaredElement };
      }
      return EmptyList<IDeclaredElement>.InstanceList;
    }

    public bool IsContextApplicable(IDataContext dataContext)
    {
      var reference = dataContext.GetData(DataConstants.REFERENCE);
      if (reference == null)
        return false;
      var rer = reference as INitraAst;
      if (rer != null)
      {
        return true;
      }
      return false;
    }
  }
}