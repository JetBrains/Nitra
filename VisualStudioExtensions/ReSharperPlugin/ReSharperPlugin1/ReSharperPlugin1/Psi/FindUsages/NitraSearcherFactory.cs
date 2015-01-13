using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
using JetBrains.ReSharper.Psi.Impl.Search.SearchDomain;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.Test;
using JetBrains.Util;

namespace JetBrains.Nitra.FindUsages
{
  [PsiSharedComponent]
  internal class NitraSearcherFactory : IDomainSpecificSearcherFactory
  {
    private readonly SearchDomainFactory mySearchDomainFactory;

    public NitraSearcherFactory(SearchDomainFactory searchDomainFactory)
    {
      mySearchDomainFactory = searchDomainFactory;
    }

    public IEnumerable<string> GetAllPossibleWordsInFile(IDeclaredElement element)
    {
      var res = new[] {element.ShortName, element.ShortName.ToLowerInvariant(), element.ShortName.ToUpperInvariant()};
      return res.Distinct();
    }

    public bool IsCompatibleWithLanguage(PsiLanguageType languageType)
    {
      return languageType.Is<DslLanguage>();
    }

    public IDomainSpecificSearcher CreateTextOccurenceSearcher(ICollection<IDeclaredElement> elements)
    {
      return new NitraTextOccurenceSearcher(elements.SelectMany(GetAllPossibleWordsInFile));
    }

    public IDomainSpecificSearcher CreateTextOccurenceSearcher(string subject)
    {
      return new NitraTextOccurenceSearcher(Enumerable.Repeat(subject, 1));
    }

    public IDomainSpecificSearcher CreateReferenceSearcher(ICollection<IDeclaredElement> elements, bool findCandidates)
    {
      return new NitraReferenceSearcher(this, elements, findCandidates, false);
    }

    public IDomainSpecificSearcher CreateLateBoundReferenceSearcher(ICollection<IDeclaredElement> elements)
    {
      return new NitraReferenceSearcher(this, elements, true, true);
    }

    public IDomainSpecificSearcher CreateConstantExpressionSearcher(ConstantValue constantValue, bool onlyLiteralExpression)
    {
      return new ConstantExpressionDomainSpecificSearcher<DslLanguage>(constantValue, onlyLiteralExpression);
    }

    public IDomainSpecificSearcher CreateConstructorSpecialReferenceSearcher(ICollection<IConstructor> constructors)
    {
      return null;
    }

    public IDomainSpecificSearcher CreateMethodsReferencedByDelegateSearcher(IDelegate @delegate)
    {
      return null;
    }

    public IDomainSpecificSearcher CreateAnonymousTypeSearcher(IList<AnonymousTypeDescriptor> typeDescription, bool caseSensitive)
    {
      return null;
    }

    public JetTuple<ICollection<IDeclaredElement>, Predicate<IFindResultReference>, bool> GetDerivedFindRequest(IFindResultReference result)
    {
      return null;
    }

    public JetTuple<ICollection<IDeclaredElement>, bool> GetNavigateToTargets(IDeclaredElement element)
    {
      return null;
    }

    public ICollection<FindResult> TransformNavigationTargets(ICollection<FindResult> targets)
    {
      return targets;
    }

    public ISearchDomain GetDeclaredElementSearchDomain(IDeclaredElement declaredElement)
    {
      if (declaredElement is NitraDeclaredElement)
        return mySearchDomainFactory.CreateSearchDomain(declaredElement.GetSolution(), false);

      return EmptySearchDomain.Instance;
    }

    public IEnumerable<RelatedDeclaredElement> GetRelatedDeclaredElements(IDeclaredElement element)
    {
      yield break;
    }
  }
}
