
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using Microsoft.VisualStudio.Language.Intellisense;
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

  [Name("Navigate To Nitra Symbol"), Order(After = "Navigate To Class"), Export(typeof(FilterDefinition))]
  internal sealed class NavigateToNitraSymbolFilter : KindFilterDefinition
  {
    public NavigateToNitraSymbolFilter()
    {
      base.Kind = "NitraSymbol";
      //base.Button = new ButtonDefinition(KnownMonikers.IntellisenseKeyword, "desc", null);
    }
  }

  class NitraNavigateToItemDisplay : INavigateToItemDisplay, INavigateToItemDisplay2
  {
    private IGlyphService _glyphService;
    private NavigateToItem _item;
    private IServiceProvider _serviceProvider;

    public NitraNavigateToItemDisplay(IServiceProvider serviceProvider, IGlyphService glyphService, NavigateToItem item)
    {
      _serviceProvider = serviceProvider;
      _glyphService = glyphService;
      _item = item;
    }

    public string AdditionalInformation => "ssss";
    public string Description => null;

    public ReadOnlyCollection<DescriptionItem> DescriptionItems
    {
      get
      {
        var category = new ReadOnlyCollection<DescriptionRun>(new[] { new DescriptionRun(_item.Name + " category", bold:true) });
        var details  = new ReadOnlyCollection<DescriptionRun>(new[] { new DescriptionRun(_item.Name + " details", Color.DarkGoldenrod) });
        var items    = new List<DescriptionItem>() { new DescriptionItem(category, details) };
        return items.AsReadOnly();
      }
    }

    public Icon Glyph => null;
    public string Name => _item.Name;
    public int GetProvisionalViewingStatus()
    {
      //VsShellUtilities.GetProvisionalViewingStatus()
      return (int)__VSPROVISIONALVIEWINGSTATUS.PVS_Enabled;
    }

    public void NavigateTo()
    {
    }

    public void PreviewItem()
    {
    }
  }
}
