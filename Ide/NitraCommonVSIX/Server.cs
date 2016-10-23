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
using Nitra.VisualStudio.BraceMatching;

namespace Nitra.VisualStudio
{
  class Server : IDisposable
  {
    Ide.Config _config;
    Dictionary<int, ITextBuffer> _bufferMap = new Dictionary<int, ITextBuffer>();

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
    public ImmutableArray<SpanClassInfo> SpanClassInfos { get { return _spanClassInfos; } }


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

    internal void CaretPositionChanged(int id, int pos, int version)
    {
      Client.Send(new ClientMessage.SetCaretPos(id, version, pos));
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

      _bufferMap[id] = textBuffer;

      var props = textBuffer.Properties;
      props.RemoveProperty(Constants.ServerKey);
      props.AddProperty(Constants.ServerKey, this);
      props.RemoveProperty(Constants.FileIdKey);
      props.AddProperty(Constants.FileIdKey, id);

      Client.ResponseMap[id] = msg => wpfTextView.VisualElement.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
        new Action<AsyncServerMessage>(msg2 => Response(textBuffer, msg2)), msg);

      textBuffer.Changed += TextBuffer_Changed;

      Client.Send(new ClientMessage.FileActivated(id));

      var pointOpt = wpfTextView.Caret.Position.Point.GetPoint(textBuffer, wpfTextView.Caret.Position.Affinity);
      if (pointOpt.HasValue)
      {
        var point = pointOpt.Value;
        CaretPositionChanged(id, point.Position, point.Snapshot.Version.VersionNumber - 1);
      }
    }

    void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
    {
      var textBuffer = (ITextBuffer)sender;
      var newVersion = e.AfterVersion.VersionNumber - 1;
      var id         = textBuffer.Properties.GetProperty<int>(Constants.FileIdKey);
      var changes    = e.Changes;

      if (changes.Count == 1)
        Client.Send(new ClientMessage.FileChanged(id, newVersion, Convert(changes[0])));
      else
      {
        var builder = ImmutableArray.CreateBuilder<FileChange>(changes.Count);

        foreach (var change in changes)
          builder.Add(Convert(change));

        Client.Send(new ClientMessage.FileChangedBatch(id, newVersion, builder.MoveToImmutable()));
      }
    }

    static FileChange Convert(ITextChange change)
    {
      var newLength = change.NewLength;
      var oldLength = change.OldLength;

      if (oldLength == 0 && newLength > 0)
        return new FileChange.Insert(change.OldPosition, change.NewText);
      if (oldLength > 0 && newLength == 0)
        return new FileChange.Delete(VsUtils.Convert(change.OldSpan));

      return new FileChange.Replace(VsUtils.Convert(change.OldSpan), change.NewText);
    }

    internal void FileDeactivated(int id)
    {
      var textBuffer =  _bufferMap[id];
      _bufferMap.Remove(id);
      textBuffer.Changed -= TextBuffer_Changed;

      Action<AsyncServerMessage> value;
      Client.ResponseMap.TryRemove(id, out value);
      Client.Send(new ClientMessage.FileDeactivated(id));

      textBuffer.Properties.RemoveProperty(Constants.FileIdKey);
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
      AsyncServerMessage.MatchedBrackets matchedBrackets;

      if ((outlining = msg as AsyncServerMessage.OutliningCreated) != null)
      {
        var tegget = (OutliningTagger)textBuffer.Properties.GetProperty(Constants.OutliningTaggerKey);
        tegget.Update(outlining);
      }
      else if ((keywordHighlighting = msg as AsyncServerMessage.KeywordsHighlightingCreated) != null)
        UpdateSpanInfos(textBuffer, HighlightingType.Keyword, keywordHighlighting.spanInfos, keywordHighlighting.Version);
      else if ((symbolsHighlighting = msg as AsyncServerMessage.SymbolsHighlightingCreated) != null)
        UpdateSpanInfos(textBuffer, HighlightingType.Symbol, symbolsHighlighting.spanInfos, symbolsHighlighting.Version);
      else if ((matchedBrackets = msg as AsyncServerMessage.MatchedBrackets) != null)
      {
        if (!textBuffer.Properties.ContainsProperty(Constants.BraceMatchingTaggerKey))
          return;
        var tegget = (NitraBraceMatchingTagger)textBuffer.Properties.GetProperty(Constants.BraceMatchingTaggerKey);
        tegget.Update(matchedBrackets);
      }

    }

    void UpdateSpanInfos(ITextBuffer textBuffer, HighlightingType highlightingType, ImmutableArray<SpanInfo> spanInfos, int version)
    {
      if (!textBuffer.Properties.ContainsProperty(Constants.NitraEditorClassifierKey))
        return;

      var tegget = (NitraEditorClassifier)textBuffer.Properties.GetProperty(Constants.NitraEditorClassifierKey);
      tegget.Update(highlightingType, spanInfos, version);
    }
  }
}
