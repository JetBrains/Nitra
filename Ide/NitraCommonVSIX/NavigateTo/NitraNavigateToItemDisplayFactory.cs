
using System;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Nitra.VisualStudio.NavigateTo
{
  [Export(typeof(INavigateToItemDisplayFactory))]
  class NitraNavigateToItemDisplayFactory : INavigateToItemDisplayFactory
  {
    readonly IGlyphService    _glyphService;
    readonly IServiceProvider _serviceProvider;
    readonly ServerModel      _serverModel;

    public NitraNavigateToItemDisplayFactory(IServiceProvider serviceProvider, IGlyphService glyphService, ServerModel serverModel)
    {
      _serviceProvider = serviceProvider;
      _glyphService    = glyphService;
      _serverModel     = serverModel;
    }

    public INavigateToItemDisplay CreateItemDisplay(NavigateToItem item)
    {
      return new NitraNavigateToItemDisplay(_serviceProvider, _glyphService, item, _serverModel);
    }
  }
}
