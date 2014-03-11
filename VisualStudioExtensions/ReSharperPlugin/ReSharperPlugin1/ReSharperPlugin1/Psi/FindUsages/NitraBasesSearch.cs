using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.ComponentModel;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.Navigation.ContextNavigation;
using JetBrains.ReSharper.Psi;
using JetBrains.Test;

namespace JetBrains.Nitra.FindUsages
{
  [ShellFeaturePart]
  public class NitraBasesSearch : FindUsagesContextSearch
  {
    public NitraBasesSearch()
    {

    }

    private readonly NitraContextSearchImpl mySearch = new NitraContextSearchImpl();

    protected override IList<IDeclaredElement> GetCandidates(IDataContext context)
    {
      return mySearch.GetCandidates(context).Where(IsCandidate).ToList();
    }

    private static bool IsCandidate(IDeclaredElement element)
    {
      var result = element is NitraDeclaredElement;
      return result;
    }

    public override bool IsContextApplicable(IDataContext dataContext)
    {
      return mySearch.IsContextApplicable(dataContext);
    }
  }
}