using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text.Editor;
using Nitra.ClientServer.Messages;
using static Nitra.ClientServer.Messages.AsyncServerMessage;
using Nitra.VisualStudio.BraceMatching;
using Nitra.VisualStudio.KeyBinding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Shell.Interop;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;
using System.Windows.Controls;

namespace Nitra.VisualStudio.Models
{
  class TextViewModel : IEquatable<TextViewModel>, IDisposable
  {
    public   FileModel                FileModel { get; }
    readonly IWpfTextView             _wpfTextView;
             InteractiveHighlightingTagger _braceMatchingTaggerOpt;
    public   MatchedBrackets          MatchedBrackets      { get; private set; }
    public   FindSymbolReferences     FindSymbolReferences { get; private set; }
    readonly KeyBindingCommandFilter  _keyBindingCommandFilter;
             SnapshotPoint?           _lastMouseHoverPointOpt;

    public TextViewModel(IWpfTextView wpfTextView, FileModel file)
    {
      _wpfTextView = wpfTextView;
      FileModel    = file;

      _wpfTextView.MouseHover += _wpfTextView_MouseHover;

      _keyBindingCommandFilter = new KeyBindingCommandFilter(wpfTextView, file.Server.ServiceProvider, this);
    }

    public InteractiveHighlightingTagger BraceMatchingTaggerOpt
    {
      get
      {
        if (_braceMatchingTaggerOpt == null)
        {
          var props = _wpfTextView.TextBuffer.Properties;
          if (!props.ContainsProperty(Constants.BraceMatchingTaggerKey))
            return null;

          _braceMatchingTaggerOpt = props.GetProperty<InteractiveHighlightingTagger>(Constants.BraceMatchingTaggerKey);
        }

        return _braceMatchingTaggerOpt;
      }
    }

    public bool Equals(TextViewModel other)
    {
      return _wpfTextView.Equals(other._wpfTextView);
    }

    public override bool Equals(object obj)
    {
      var other = obj as TextViewModel;

      if (other != null)
        return Equals(other);

      return false;
    }

    public override int GetHashCode()
    {
      return _wpfTextView.GetHashCode();
    }

    public override string ToString()
    {
      return _wpfTextView.ToString();
    }

    public void Dispose()
    {
      _keyBindingCommandFilter.Dispose();
      _wpfTextView.Properties.RemoveProperty(Constants.TextViewModelKey);
    }

    internal void Reset()
    {
      Update(default(MatchedBrackets));
    }

    internal void Update(MatchedBrackets matchedBrackets)
    {
      MatchedBrackets = matchedBrackets;
      BraceMatchingTaggerOpt?.Update();
    }

    internal void Update(FindSymbolReferences findSymbolReferences)
    {
      FindSymbolReferences = findSymbolReferences;
      BraceMatchingTaggerOpt?.Update();
    }

    internal void NavigateTo(ITextSnapshot snapshot, int pos)
    {
      _wpfTextView.Caret.MoveTo(new SnapshotPoint(snapshot, pos));
      _wpfTextView.ViewScroller.EnsureSpanVisible(new SnapshotSpan(snapshot, pos, 0));
    }

    internal void NavigateTo(SnapshotPoint snapshotPoint)
    {
      _wpfTextView.Caret.MoveTo(snapshotPoint);
      _wpfTextView.ViewScroller.EnsureSpanVisible(new SnapshotSpan(snapshotPoint, snapshotPoint));
    }

    internal void Navigate(int line, int column)
    {
      Navigate(_wpfTextView.TextBuffer.CurrentSnapshot, line, column);
    }

    internal void Navigate(ITextSnapshot snapshot, int line, int column)
    {
      var snapshotLine  = snapshot.GetLineFromLineNumber(line);
      var snapshotPoint = snapshotLine.Start + column;
      NavigateTo(snapshotPoint);
      _wpfTextView.ToVsTextView().SendExplicitFocus();
    }

    internal void GotoRef(SnapshotPoint point)
    {
      var fileModel = FileModel;
      var client = fileModel.Server.Client;
      client.Send(new ClientMessage.FindSymbolReferences(fileModel.Id, point.Snapshot.Version.Convert(), point.Position));
      var msg = client.Receive<ServerMessage.FindSymbolReferences>();

      var locs = msg.symbols.SelectMany(s => s.Definitions.Select(d => d.Location).Concat(s.References.SelectMany(
        referenc => referenc.Ranges.Select(range => new Location(referenc.File, range))))).ToArray();
      ShowInFindResultWindow(fileModel, msg.referenceSpan, locs);
    }

    internal void GotoDefn(SnapshotPoint point)
    {
      var fileModel = FileModel;
      var client = fileModel.Server.Client;
      client.Send(new ClientMessage.FindSymbolDefinitions(fileModel.Id, point.Snapshot.Version.Convert(), point.Position));
      var msg = client.Receive<ServerMessage.FindSymbolDefinitions>();
      var len = msg.definitions.Length;

      ShowInFindResultWindow(fileModel, msg.referenceSpan, msg.definitions.Select(d => d.Location).ToArray());
    }

    void _wpfTextView_MouseHover(object sender, MouseHoverEventArgs e)
    {
      var pointOpt = e.TextPosition.GetPoint(_wpfTextView.TextBuffer, PositionAffinity.Predecessor);

      _lastMouseHoverPointOpt = pointOpt;

      if (!pointOpt.HasValue)
        return;

      var point    = pointOpt.Value;
      var snapshot = point.Snapshot;
      var server   = this.FileModel.Server;

      this.FileModel.OnMouseHover(this);

      server.Client.Send(new ClientMessage.GetHint(this.FileModel.Id, snapshot.Version.Convert(), point.Position));
    }

    internal void ShowHint(AsyncServerMessage.Hint msg)
    {
      if (!_lastMouseHoverPointOpt.HasValue)
        return;

      var lastPoint = _lastMouseHoverPointOpt.Value;
      var snapshot  = _wpfTextView.TextBuffer.CurrentSnapshot;

      this.FileModel.OnMouseHover(null);
      _lastMouseHoverPointOpt = null;

      if (snapshot.Version.Convert() != msg.Version || lastPoint.Snapshot != snapshot)
        return;

      if (!msg.referenceSpan.IntersectsWith(lastPoint.Position))
        return;

      // TODO: Use VS colors. .SetResourceReference(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolTipBrushKey); //ToolTipTextBrushKey 

      //Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolTipBrushKey

      var span       = new SnapshotSpan(snapshot, new Span(msg.referenceSpan.StartPos, msg.referenceSpan.Length));
      var geometry   = _wpfTextView.TextViewLines.GetTextMarkerGeometry(span);

      if (geometry == null)
        return;

      var visual     = (Visual)_wpfTextView;
      var bound      = geometry.Bounds;

      bound.Offset(-_wpfTextView.ViewportLeft, -_wpfTextView.ViewportTop);
      bound.Height  += 10;

      var rect = new Rect(visual.PointToScreen(bound.Location), bound.Size);
      var vsTextView = _wpfTextView.ToVsTextView();
      var hWnd       = vsTextView.GetWindowHandle();
      var hintXml    = msg.text;
      var hint       = this.FileModel.Server.Hint;

      hint.BackgroundResourceReference = EnvironmentColors.ToolTipBrushKey;
      hint.ForegroundResourceReference = EnvironmentColors.ToolTipTextBrushKey;

      if (hint.IsOpen)
      {
        if (hint.PlacementRect == rect)
        {
          hint.Text = hintXml;
          return;
        }

        hint.Close();
      }

      hint.Show(hWnd, rect, SubHintText, hintXml, this.SpanClassToBrush);
    }

    Brush SpanClassToBrush(string spanClass)
    {
      return this.FileModel.SpanClassToBrush(spanClass, _wpfTextView);
    }

    string SubHintText(string symbolIdText)
    {
      var symbolId = int.Parse(symbolIdText);
      var fileModel = FileModel;
      var client = fileModel.Server.Client;
      client.Send(new ClientMessage.GetSubHint(GetCurrntProjectId(), symbolId));
      var msg = client.Receive<ServerMessage.SubHint>();
      return msg.text;
    }

    ProjectId GetCurrntProjectId()
    {
      var project = FileModel.Hierarchy.GetProject();
      return new ProjectId(FileModel.Server.Client.StringManager.GetId(project.FileName));
    }

    static void GoToLocation(FileModel fileModel, Location loc)
    {
      if (loc.File.FileId < 0)
        return;

      var path = fileModel.Server.Client.StringManager.GetPath(loc.File.FileId);
      fileModel.Server.ServiceProvider.Navigate(path, loc.Range.StartLine, loc.Range.StartColumn);
    }

    void ShowInFindResultWindow(FileModel fileModel, NSpan span, Location[] locations)
    {
      if (locations.Length == 1)
      {
        GoToLocation(fileModel, locations[0]);
        return;
      }

      var findSvc = (IVsObjectSearch)fileModel.Server.ServiceProvider.GetService(typeof(SVsObjectSearch));
      Debug.Assert(findSvc != null);

      var caption = _wpfTextView.TextBuffer.CurrentSnapshot.GetText(VsUtils.Convert(span));

      var libSearchResults = new LibraryNode("<Nitra>", LibraryNode.LibraryNodeType.References, LibraryNode.LibraryNodeCapabilities.None, null);

      foreach (var location in locations)
      {
        var inner = new GotoInfoLibraryNode(location, caption, fileModel.Server);
        libSearchResults.AddNode(inner);
      }

      var package = NitraCommonVsPackage.Instance;
      package.SetFindResult(libSearchResults);
      var criteria =
        new[]
        {
          new VSOBSEARCHCRITERIA
          {
            eSrchType = VSOBSEARCHTYPE.SO_ENTIREWORD,
            grfOptions = (uint)_VSOBSEARCHOPTIONS.VSOBSO_CASESENSITIVE,
            szName = "<dummy>",
            dwCustom = Library.FindAllReferencesMagicNum,
          }
        };

      IVsObjectList results;
      var hr = findSvc.Find((uint)__VSOBSEARCHFLAGS.VSOSF_EXPANDREFS, criteria, out results);
    }
  }
}
