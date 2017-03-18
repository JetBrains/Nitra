using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

using Nitra.ClientServer.Client;
using Nitra.ClientServer.Messages;
using Nitra.VisualStudio.Models;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Windows.Media;

using WpfHint2;

using Ide = NitraCommonIde;
using M = Nitra.ClientServer.Messages;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using Nitra.VisualStudio.NavigateTo;
using System.IO;

namespace Nitra.VisualStudio
{
  /// <summary>Represent a server (Nitra.ClientServer.Server) instance.</summary>
  internal class ServerModel : IDisposable
  {
              Ide.Config                    _config;
    public    IServiceProvider              ServiceProvider   { get; }
    public    NitraClient                   Client            { get; private set; }
    public    Hint                          Hint              { get; } = new Hint() { WrapWidth = 900.1 };
    public    ImmutableHashSet<string>      Extensions        { get; }

    public    bool                          IsLoaded          { get; private set; }
    public    bool                          IsSolutionCreated { get; private set; }
              ImmutableArray<SpanClassInfo> _spanClassInfos   = ImmutableArray<SpanClassInfo>.Empty;
    readonly  HashSet<FileModel>            _fileModels       = new HashSet<FileModel>();
    readonly  Dictionary<FileId, FileModel> _filIdToFileModelMap = new Dictionary<FileId, FileModel>();
    private   INavigateToCallback           _callback;
    private   NitraNavigateToItemProvider   _nitraNavigateToItemProvider;

    public ServerModel(StringManager stringManager, Ide.Config config, IServiceProvider serviceProvider)
    {
      Contract.Requires(stringManager != null);
      Contract.Requires(config != null);
      Contract.Requires(serviceProvider != null);

      ServiceProvider = serviceProvider;

      var client = new NitraClient(stringManager);
      client.Send(new ClientMessage.CheckVersion(M.Constants.AssemblyVersionGuid));
      var responseMap = client.ResponseMap;
      responseMap[-1] = Response;
      _config = config;
      Client = client;

      var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var lang in config.Languages)
        builder.UnionWith(lang.Extensions);
      Extensions = builder.ToImmutable();
    }

    public ImmutableArray<SpanClassInfo> SpanClassInfos { get { return _spanClassInfos; } }

    private static M.Config ConvertConfig(Ide.Config config)
    {
      var ps = config.ProjectSupport;
      var projectSupport = new M.ProjectSupport(ps.Caption, ps.TypeFullName, ps.Path);
      var languages = config.Languages.Select(x => new M.LanguageInfo(x.Name, x.Path, new M.DynamicExtensionInfo[0])).ToArray();
      var msgConfig = new M.Config(projectSupport, languages, new string[0]);
      return msgConfig;
    }

    FileModel GetFileModelOpt(FileId fileId)
    {
      if (_filIdToFileModelMap.TryGetValue(fileId, out var fileModel))
        return fileModel;
      return null;
    }

    internal void Add(FileModel fileModel)
    {
      _fileModels.Add(fileModel);
      _filIdToFileModelMap.Add(fileModel.Id, fileModel);
    }

    internal void Remove(FileModel fileModel)
    {
      _fileModels.Remove(fileModel);
      _filIdToFileModelMap.Remove(fileModel.Id);
    }

    internal void SolutionStartLoading(SolutionId id, string solutionPath)
    {
      Debug.Assert(!IsSolutionCreated);
      Client.Send(new ClientMessage.SolutionStartLoading(id, solutionPath));
      IsSolutionCreated = true;
    }

    internal void SolutionLoaded(SolutionId solutionId)
    {
      Debug.Assert(IsSolutionCreated);
      Client.Send(new ClientMessage.SolutionLoaded(solutionId));

      //foreach (var fileModel in _fileModels)
      //  fileModel.Activate();

      //IsLoaded = true;
    }

    internal void ProjectStartLoading(ProjectId id, string projectPath)
    {
      Debug.Assert(IsSolutionCreated);
      var config = ConvertConfig(_config);
      Client.Send(new ClientMessage.ProjectStartLoading(id, projectPath, config));
    }

    internal void ProjectLoaded(ProjectId id)
    {
      Debug.Assert(IsSolutionCreated);
      IsLoaded = true;
      Client.Send(new ClientMessage.ProjectLoaded(id));
    }

    internal void ReferenceAdded(ProjectId projectId, string referencePath)
    {
      Debug.Assert(IsSolutionCreated);
      Client.Send(new ClientMessage.ReferenceLoaded(projectId, "File:" + referencePath));
    }

    internal void ProjectReferenceAdded(ProjectId projectId, ProjectId referencedProjectId, string referencePath)
    {
      Debug.Assert(IsSolutionCreated);
      Client.Send(new ClientMessage.ProjectReferenceLoaded(projectId, referencedProjectId, referencePath));
    }

    internal void AddedMscorlibReference(ProjectId projectId)
    {
      Debug.Assert(IsSolutionCreated);
      Client.Send(new ClientMessage.ReferenceLoaded(projectId, "FullName:mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"));
    }

    internal void BeforeCloseProject(ProjectId id)
    {
      Debug.Assert(IsSolutionCreated);
      Client.Send(new ClientMessage.ProjectUnloaded(id));
    }

    internal void FileAdded(ProjectId projectId, string path, FileId id, FileVersion version)
    {
      Debug.Assert(IsSolutionCreated);
      Client.Send(new ClientMessage.FileLoaded(projectId, path, id, version));
    }

    internal void FileUnloaded(FileId id)
    {
      foreach (var fileModel in _fileModels)
      {
        if (fileModel.Id == id)
        {
          fileModel.Remove();
          return;
        }
      }
      Client.Send(new ClientMessage.FileUnloaded(id));
    }

    internal void ViewActivated(IWpfTextView wpfTextView, FileId id, IVsHierarchy hierarchy, string fullPath)
    {
      Debug.Assert(IsSolutionCreated);
      var textBuffer = wpfTextView.TextBuffer;

      TryAddServerProperty(textBuffer);

      FileModel fileModel = VsUtils.GetOrCreateFileModel(wpfTextView, id, this, hierarchy, fullPath);
      TextViewModel textViewModel = VsUtils.GetOrCreateTextViewModel(wpfTextView, fileModel);

      fileModel.ViewActivated(textViewModel);
    }

    void TryAddServerProperty(ITextBuffer textBuffer)
    {
      if (!textBuffer.Properties.ContainsProperty(Constants.ServerKey))
        textBuffer.Properties.AddProperty(Constants.ServerKey, this);
    }

    internal void ViewDeactivated(IWpfTextView wpfTextView, FileId id)
    {
      //if (wpfTextView.TextBuffer.Properties.TryGetProperty<FileModel>(Constants.FileModelKey, out var fileModel))
      //  fileModel.Remove(wpfTextView);
    }

    internal void DocumentWindowDestroy(IWpfTextView wpfTextView)
    {
      FileModel fileModel;
      if (wpfTextView.TextBuffer.Properties.TryGetProperty<FileModel>(Constants.FileModelKey, out fileModel))
        fileModel.Dispose();
    }

    void Response(AsyncServerMessage msg)
    {
      switch (msg)
      {
        case AsyncServerMessage.LanguageLoaded languageInfo:
          var spanClassInfos = languageInfo.spanClassInfos;
          if (_spanClassInfos.IsDefaultOrEmpty)
            _spanClassInfos = spanClassInfos;
          else if (!spanClassInfos.IsDefaultOrEmpty)
          {
            var bilder = ImmutableArray.CreateBuilder<SpanClassInfo>(_spanClassInfos.Length + spanClassInfos.Length);
            bilder.AddRange(_spanClassInfos);
            bilder.AddRange(spanClassInfos);
            _spanClassInfos = bilder.MoveToImmutable();
          }
          break;

        case AsyncServerMessage.FindSymbolReferences findSymbolReferences:
          // передать всем вьюхам отображаемым на экране

          foreach (var fileModel in _fileModels)
            foreach (var textViewModel in fileModel.TextViewModels)
              textViewModel.Update(findSymbolReferences);
          break;

        case AsyncServerMessage.FoundDeclarations found:
          if (_callback == null)
            break;

          MatchKind calcKibd(DeclarationInfo decl)
          {
            var spans = decl.NameMatchRuns;
            switch (spans.Length)
            {
              case 0: return MatchKind.None;
              case 1:
                var name = decl.Name;
                var span = decl.NameMatchRuns[0];
                if (span.StartPos == 0)
                  return span.Length == name.Length ? MatchKind.Exact : MatchKind.Prefix;
                return MatchKind.Substring;
              default:
                return MatchKind.Regular;
            }
          }

          foreach (var decl in found.declarations)
          {
            // So far we can use the following kinds "OtherSymbol", "NitraSymbol"
            // TODO: Allows add user spetified kinds
            var loc = decl.Location;
            var fileId = loc.File.FileId;
            var path = fileId == FileId.Invalid ? "<no file>" : Client.StringManager.GetPath(fileId);
            var ext  = fileId == FileId.Invalid ? "" : Path.GetExtension(path);
            var lang = _config.Languages.Where(x => x.Extensions.Contains(ext)).Select(x => x.Name).SingleOrDefault() ?? "<Unknown Nitra language>";
            _callback.AddItem(new NavigateToItem(decl.Name, "NitraSymbol", lang, decl.FullName, decl, calcKibd(decl), false, _nitraNavigateToItemProvider.Factory));
          }

          break;
      }
    }

    internal SpanClassInfo? GetSpanClassOpt(string spanClass)
    {
      foreach (var spanClassInfo in SpanClassInfos)
        if (spanClassInfo.FullName == spanClass)
          return spanClassInfo;

      return null;
    }

    internal Brush SpanClassToBrush(string spanClass)
    {
      var spanClassOpt = GetSpanClassOpt(spanClass);
      if (spanClassOpt.HasValue)
      {
        // TODO: use classifiers
        var bytes = BitConverter.GetBytes(spanClassOpt.Value.ForegroundColor);
        return new SolidColorBrush(Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]));
      }

      return Brushes.Black;
    }

    public bool IsSupportedExtension(string ext)
    {
      return Extensions.Contains(ext);
    }

    internal void StartSearch(NitraNavigateToItemProvider nitraNavigateToItemProvider, INavigateToCallback callback, string pattern, bool hideExternalItems, bool searchCurrentDocument, ISet<string> kinds)
    {
      _callback                     = callback;
      _nitraNavigateToItemProvider  = nitraNavigateToItemProvider;
      // TODO: find curent ProjectId for active file
      // TODO: Add support for 'searchCurrentDocument'
      Client.Send(new ClientMessage.FindDeclarations(pattern, ProjectId.Invalid, hideExternalItems, kinds.ToImmutableArray()));
    }

    internal void StopSearch()
    {
      _callback = null;
      _nitraNavigateToItemProvider = null;
    }

    public void Dispose()
    {
      var fileModels = _fileModels.ToArray();
      foreach (var fileModel in fileModels)
        fileModel.Dispose();

      Client?.Dispose();
    }
  }
}
