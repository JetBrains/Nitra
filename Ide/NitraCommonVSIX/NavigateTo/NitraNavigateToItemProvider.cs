
using System;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Nitra.VisualStudio.NavigateTo
{
  class NitraNavigateToItemProvider : INavigateToItemProvider
  {
    readonly IServiceProvider _serviceProvider;
    readonly IGlyphService    _glyphService;

    public NitraNavigateToItemProvider(IServiceProvider serviceProvider, IGlyphService glyphService1)
    {
      _serviceProvider = serviceProvider;
      _glyphService    = glyphService1;
    }

    public void StartSearch(INavigateToCallback callback, string searchValue)
    {
      var factory = new NitraNavigateToItemDisplayFactory(_serviceProvider, _glyphService);
      callback.AddItem(new NavigateToItem("RulePrefix",  "rule", "Nitra", "aaa", null, MatchKind.Prefix, factory));
      callback.AddItem(new NavigateToItem("ExactRule", "rule", "Nitra", "aaa", null, MatchKind.Exact, factory));
      callback.AddItem(new NavigateToItem("RuleRegular", "rule", "Nitra", "aaa", null, MatchKind.Regular, factory));
      callback.AddItem(new NavigateToItem("SubstringRule", "rule", "Nitra", "aaa", null, MatchKind.Substring, factory));
      callback.AddItem(new NavigateToItem("None", "rule", "Nitra", "aaa", null, MatchKind.None, factory));
      //callback.Done();
    }

    public void StopSearch()
    {
    }

    public void Dispose()
    {
    }
  }
}
