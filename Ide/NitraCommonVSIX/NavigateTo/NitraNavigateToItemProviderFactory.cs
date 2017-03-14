
using System;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Nitra.VisualStudio.NavigateTo
{
  [Export(typeof(INavigateToItemProviderFactory))]
  class NitraNavigateToItemProviderFactory : INavigateToItemProviderFactory
  {
    readonly IGlyphService _glyphService;

    [ImportingConstructor]
    public NitraNavigateToItemProviderFactory(IGlyphService glyphService)
    {
      _glyphService = glyphService;
    }

    public bool TryCreateNavigateToItemProvider(IServiceProvider serviceProvider, out INavigateToItemProvider provider)
    {
      provider = new NitraNavigateToItemProvider(serviceProvider, _glyphService);
      return true;
    }
  }
}
