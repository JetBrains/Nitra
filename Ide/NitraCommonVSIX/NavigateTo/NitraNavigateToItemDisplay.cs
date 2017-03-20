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
    public string           AdditionalInformation => Declaration.Kind + " " + Declaration.FullName + " – " + FileName + LineCol;

    private bool   FileValid   => Declaration.Location.File.FileId != FileId.Invalid;
    private string FullName    => FileValid ? _serverModel.Client.StringManager.GetPath(Declaration.Location.File.FileId) : "<no file>";
    private string FileName    => FileValid ? Path.GetFileName(FullName) : "<no file>";
    private Range  Range       => Declaration.Location.Range;
    private string LineCol     => "(" + Range.StartLine + "," + Range.StartColumn + ")";
    public  string Description => null;

    public ReadOnlyCollection<DescriptionItem> DescriptionItems
    {
      get
      {
        DescriptionItem Make(string category, params DescriptionRun[] details)
        {
          return new DescriptionItem(
            new ReadOnlyCollection<DescriptionRun>(new[] { new DescriptionRun(category, true) }),
            new ReadOnlyCollection<DescriptionRun>(details));
        }

        var items = new List<DescriptionItem>()
        {
          Make("Name:",      MakeNameDescriptionRuns()),
          Make("File:",      new DescriptionRun(FullName)),
          Make("Line:",      new DescriptionRun(Range.StartLine.ToString())),
          Make("Kind:",      new DescriptionRun(Declaration.Kind, Color.Blue)),
          Make("Full name:", new DescriptionRun(Declaration.FullName)),
        };
        return items.AsReadOnly();
      }
    }

    DescriptionRun[] MakeNameDescriptionRuns()
    {
      var spanClassOpt = _serverModel.GetSpanClassOpt(Declaration.SpanClassId);
      var color = spanClassOpt.HasValue ? spanClassOpt.Value.ToDColor() : Color.Black;

      var name = Declaration.Name;
      var nameParts = new List<DescriptionRun>();
      void t(int start, int len) => nameParts.Add(new DescriptionRun(name.Substring(start, len), color, false, false, false));
      void b(int start, int len) => nameParts.Add(new DescriptionRun(name.Substring(start, len), color, true,  false, false));

      var prev = 0;
      foreach (var r in Declaration.NameMatchRuns)
      {
        if (r.StartPos > prev)
          t(prev, r.StartPos - prev);

        b(r.StartPos, r.Length);

        prev = r.EndPos;
      }

      if (name.Length > prev)
        t(prev, name.Length - prev);

      return nameParts.ToArray();
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
      VsUtils.NavigateTo(_serviceProvider, FullName, Range.Span);
    }

    public void PreviewItem()
    {
      VsUtils.NavigateTo(_serviceProvider, FullName, Range.Span);
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
