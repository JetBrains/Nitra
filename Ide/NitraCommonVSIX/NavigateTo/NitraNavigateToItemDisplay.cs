using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Drawing;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Text;
using Nitra.ClientServer.Messages;
using System.IO;

namespace Nitra.VisualStudio.NavigateTo
{
  class NitraNavigateToItemDisplay : INavigateToItemDisplay3
  {
    readonly IGlyphService    _glyphService;
    readonly NavigateToItem   _item;
    readonly IServiceProvider _serviceProvider;
    readonly ServerModel      _serverModel;

    public NitraNavigateToItemDisplay(IServiceProvider serviceProvider, IGlyphService glyphService, NavigateToItem item, ServerModel serverModel)
    {
      _serviceProvider  = serviceProvider;
      _glyphService     = glyphService;
      _item             = item;
      _serverModel      = serverModel;
    }

    public DeclarationInfo  Declaration           => (DeclarationInfo)_item.Tag;
    public string           AdditionalInformation => Declaration.FullName + " [" + FileName + LineCol + "]";

    private bool   FileValid   => Declaration.Location.File.FileId != FileId.Invalid;
    private string FullName    => FileValid ? _serverModel.Client.StringManager.GetPath(Declaration.Location.File.FileId) : "<no file>";
    private string FileName    => FileValid ? Path.GetFileName(FullName) : "<no file>";
    private Range  Range       => Declaration.Location.Range;
    private string LineCol     => "(" + Range.StartColumn + Range.StartColumn + ")";
    public  string Description => null;

    public ReadOnlyCollection<DescriptionItem> DescriptionItems
    {
      get
      {
        var category = new ReadOnlyCollection<DescriptionRun>(new[] { new DescriptionRun(_item.Name + " category", bold: true) });
        var details  = new ReadOnlyCollection<DescriptionRun>(new[] { new DescriptionRun(_item.Name + " details", Color.DarkGoldenrod) });
        var items    = new List<DescriptionItem>()                  { new DescriptionItem(category, details) };
        return items.AsReadOnly();
      }
    }

    public Icon Glyph => null;
    public string Name => _item.Name;

    public int GetProvisionalViewingStatus()
    {
      return (int)__VSPROVISIONALVIEWINGSTATUS.PVS_Enabled;
    }

    // INavigateToItemDisplay2

    public void NavigateTo()
    {
    }

    public void PreviewItem()
    {
    }

    // INavigateToItemDisplay3

    public ImageMoniker GlyphMoniker => ImageLibrary.InvalidImageMoniker;

    public IReadOnlyList<Span> GetAdditionalInformationMatchRuns(string searchValue)
    {
      return new List<Span>();
    }

    public IReadOnlyList<Span> GetNameMatchRuns(string searchValue)
    {
      var decl = (DeclarationInfo)_item.Tag;
      var spans = decl.NameMatchRuns.Select(r => new Span(r.StartPos, r.Length)).ToArray();
      return spans;
    }
  }
}
