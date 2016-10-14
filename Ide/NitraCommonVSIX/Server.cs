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

namespace Nitra.VisualStudio
{
  class Server : IDisposable
  {
    private Ide.Config _config;

    public NitraClient Client { get; private set; }

    public Server(StringManager stringManager, Ide.Config config)
    {
      var client = new NitraClient(stringManager);
      client.Send(new ClientMessage.CheckVersion(Constants.AssemblyVersionGuid));
      var responseMap = client.ResponseMap;
      responseMap[-1] = Response;
      _config = config;
      Client = client;
    }

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
      Client.ResponseMap[id] = msg => wpfTextView.VisualElement.Dispatcher.BeginInvoke(DispatcherPriority.Normal, 
        new Action<AsyncServerMessage>(msg2 => Response(wpfTextView.TextBuffer, msg2)), msg);
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
    }

    void Response(ITextBuffer textBuffer, AsyncServerMessage msg)
    {
      AsyncServerMessage.OutliningCreated outlining;

      if ((outlining = msg as AsyncServerMessage.OutliningCreated) != null)
      {
        var tegget = (OutliningTagger)textBuffer.Properties.GetProperty(Constants.OutliningTaggerKey);
        tegget.Update(outlining);
      }
    }
  }
}
