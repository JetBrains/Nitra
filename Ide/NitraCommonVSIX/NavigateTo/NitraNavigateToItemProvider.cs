
using System;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using System.Collections.Generic;

namespace Nitra.VisualStudio.NavigateTo
{
  class NitraNavigateToItemProvider : INavigateToItemProvider2
  {
    public  readonly IServiceProvider                  ServiceProvider;
    public  readonly IGlyphService                     GlyphService;

    public NitraNavigateToItemProvider(IServiceProvider serviceProvider, IGlyphService glyphService)
    {
      ServiceProvider = serviceProvider;
      GlyphService    = glyphService;
    }

    public NitraNavigateToItemDisplayFactory GetFactory(ServerModel serverModel)
    {
      return new NitraNavigateToItemDisplayFactory(ServiceProvider, GlyphService, serverModel);
    }

    public void StartSearch(INavigateToCallback callback, string searchValue)
    {
      StartSearch(callback, searchValue, true, false, new SortedSet<string>());
    }

    public void StopSearch()
    {
      var servers = NitraCommonVsPackage.Instance.Servers;
      foreach (var server in servers)
        server.StopSearch();
    }

    public void Dispose()
    {
    }

    // INavigateToItemProvider2

    public ISet<string> KindsProvided => new HashSet<string>() { "OtherSymbol", "NitraSymbol" };
    public bool         CanFilter     => true;

    public void StartSearch(INavigateToCallback callback, string searchValue, INavigateToFilterParameters filter)
    {
      var options = (INavigateToOptions2)callback.Options;
      StartSearch(callback, searchValue, options.HideExternalItems, options.SearchCurrentDocument, filter.Kinds);
    }

    private void StartSearch(INavigateToCallback callback, string pattern, bool hideExternalItems, bool searchCurrentDocument, ISet<string> kinds)
    {
      var servers = NitraCommonVsPackage.Instance.Servers;
      foreach (var server in servers)
        server.StartSearch(this, callback, pattern, hideExternalItems, searchCurrentDocument, kinds);
    }
  }
}
