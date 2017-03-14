
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Nitra.VisualStudio.NavigateTo
{
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
