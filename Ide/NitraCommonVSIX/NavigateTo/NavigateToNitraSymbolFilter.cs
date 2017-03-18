using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace Nitra.VisualStudio.NavigateTo
{
  [Name("Navigate To Nitra Symbol"), Order(After = "Navigate To Class"), Export(typeof(FilterDefinition))]
  internal sealed class NavigateToNitraSymbolFilter : KindFilterDefinition
  {
    public NavigateToNitraSymbolFilter()
    {
      base.Kind = "NitraSymbol";
      //base.Button = new ButtonDefinition(KnownMonikers.IntellisenseKeyword, "desc", null);
    }
  }
}
