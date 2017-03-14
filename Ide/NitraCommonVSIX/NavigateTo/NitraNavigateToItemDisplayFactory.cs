
using System;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Nitra.VisualStudio.NavigateTo
{
  [Export(typeof(INavigateToItemDisplayFactory))]
  class NitraNavigateToItemDisplayFactory : INavigateToItemDisplayFactory
  {
    private IGlyphService _glyphService;
    private IServiceProvider _serviceProvider;

    public NitraNavigateToItemDisplayFactory(IServiceProvider serviceProvider, IGlyphService glyphService)
    {
      _serviceProvider = serviceProvider;
      _glyphService = glyphService;
    }

    public INavigateToItemDisplay CreateItemDisplay(NavigateToItem item)
    {
      return new NitraNavigateToItemDisplay(_serviceProvider, _glyphService, item);
    }
  }
}
