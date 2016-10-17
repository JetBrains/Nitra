using Nitra.ClientServer.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NitraCommonIde;
using System.Diagnostics;
using Nitra.ClientServer.Messages;

using Ide = NitraCommonIde;
using M = Nitra.ClientServer.Messages;
using Microsoft.VisualStudio.Text.Editor;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Classification;
using System.Collections.Immutable;
using Nitra.VisualStudio.Highlighting;

namespace Nitra.VisualStudio
{
  class Server : IDisposable
  {
    private Ide.Config _config;

    public NitraClient Client { get; private set; }

    public Server(StringManager stringManager, Ide.Config config)
    {
      var client = new NitraClient(stringManager);
      client.Send(new ClientMessage.CheckVersion(M.Constants.AssemblyVersionGuid));
      var responseMap = client.ResponseMap;
      responseMap[-1] = Response;
      _config = config;
      Client = client;
    }

    private ImmutableArray<SpanClassInfo> _spanClassInfos = ImmutableArray<SpanClassInfo>.Empty;
    public  ImmutableArray<SpanClassInfo> SpanClassInfos { get { return _spanClassInfos; } }


    private static M.Config ConvertConfig(Ide.Config config)
    {
      var ps = config.ProjectSupport;
      var projectSupport = new M.ProjectSupport(ps.Caption, ps.TypeFullName, ps.Path);
      var languages = config.Languages.Select(x => new M.LanguageInfo(x.Name, x.Path, new M.DynamicExtensionInfo[0])).ToArray();
      var msgConfig = new M.Config(projectSupport, languages, new string[0]);
      return msgConfig;
    }

    public void Dispose()
    {
      Client?.Dispose();
    }

    internal void SolutionStartLoading(int id, string solutionPath)
    {
      Client.Send(new ClientMessage.SolutionStartLoading(id, solutionPath));
    }

    internal void SolutionLoaded(int solutionId)
    {
      Client.Send(new ClientMessage.SolutionLoaded(solutionId));
    }

    internal void ProjectStartLoading(int id, string projectPath)
    {
      var config = ConvertConfig(_config);
      Client.Send(new ClientMessage.ProjectStartLoading(id, projectPath, config));
    }

    internal void ProjectLoaded(int id)
    {
      Client.Send(new ClientMessage.ProjectLoaded(id));
    }

    internal void ReferenceAdded(int projectId, string referencePath)
    {
      Client.Send(new ClientMessage.ReferenceLoaded(projectId, "File:" + referencePath));
    }

    internal void BeforeCloseProject(int id)
    {
      Client.Send(new ClientMessage.ProjectUnloaded(id));
    }

    internal void FileAdded(int projectId, string path, int id, int version)
    {
      Client.Send(new ClientMessage.FileLoaded(projectId, path, id, version));
    }

    internal void FileUnloaded(int id)
    {
      Client.Send(new ClientMessage.FileUnloaded(id));
    }

    internal void FileActivated(IWpfTextView wpfTextView, int id)
    {
      var textBuffer = wpfTextView.TextBuffer;

      textBuffer.Properties.AddProperty(Constants.ServerKey, this);

      Client.ResponseMap[id] = msg => wpfTextView.VisualElement.Dispatcher.BeginInvoke(DispatcherPriority.Normal, 
        new Action<AsyncServerMessage>(msg2 => Response(textBuffer, msg2)), msg);

      Client.Send(new ClientMessage.FileActivated(id));
    }

    internal void FileDeactivated(int id)
    {
      Action<AsyncServerMessage> value;
      Client.ResponseMap.TryRemove(id, out value);
      Client.Send(new ClientMessage.FileDeactivated(id));
    }

    void Response(AsyncServerMessage msg)
    {
      AsyncServerMessage.LanguageLoaded languageInfo;

      if ((languageInfo = msg as AsyncServerMessage.LanguageLoaded) != null)
      {
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
      }
    }

    void Response(ITextBuffer textBuffer, AsyncServerMessage msg)
    {
      AsyncServerMessage.OutliningCreated outlining;
      AsyncServerMessage.KeywordsHighlightingCreated keywordHighlighting;
      AsyncServerMessage.SymbolsHighlightingCreated symbolsHighlighting;

      if ((outlining = msg as AsyncServerMessage.OutliningCreated) != null)
      {
        var tegget = (OutliningTagger)textBuffer.Properties.GetProperty(Constants.OutliningTaggerKey);
        tegget.Update(outlining);
      }
      else if ((keywordHighlighting = msg as AsyncServerMessage.KeywordsHighlightingCreated) != null)
        UpdateSpanInfos(textBuffer, HighlightingType.Keyword, keywordHighlighting.spanInfos, keywordHighlighting.Version);
      else if ((symbolsHighlighting = msg as AsyncServerMessage.SymbolsHighlightingCreated) != null)
        UpdateSpanInfos(textBuffer, HighlightingType.Symbol, symbolsHighlighting.spanInfos, symbolsHighlighting.Version);
    }

    void UpdateSpanInfos(ITextBuffer textBuffer, HighlightingType highlightingType, ImmutableArray<SpanInfo> spanInfos, int version)
    {
      var tegget = (NitraEditorClassifier)textBuffer.Properties.GetProperty(Constants.NitraEditorClassifierKey);
      tegget.Update(highlightingType, spanInfos, version);
    }
  }
}
