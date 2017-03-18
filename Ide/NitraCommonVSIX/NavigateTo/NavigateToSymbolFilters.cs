using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Imaging;

namespace Nitra.VisualStudio.NavigateTo
{
  internal class NavigateToSymbolFilters
  {
    [Name("Navigate To Nitra Symbol"), Order(After = "Navigate To Class"), Export(typeof(FilterShortcutDefinition))]
    public sealed class NitraSymbolFilterShortcut : FilterShortcutDefinition
    {
      public NitraSymbolFilterShortcut()
      {
        base.ActivationSequence = "n";
        base.IsDelimiterRequired = true;
        base.Description = "desc";
        base.Button = new ButtonDefinition(KnownMonikers.IntellisenseKeyword, "desc", null);
      }
    }

    [Filter("Navigate To All Nitra Symbol"), FilterShortcut("Navigate To Nitra Symbol"), Export]
    internal FilterToShortcutDefinition nitraSymbolFilterMapping;
  }
}
