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

namespace Nitra.VisualStudio
{
  class Server : IDisposable
  {
    private Ide.Config _config;

    public NitraClient Client { get; private set; }

    public Server(StringManager stringManager, Ide.Config config)
    {
      Client = new NitraClient(stringManager);
      _config = config;
    }

    public void Dispose()
    {
      Client?.Dispose();
    }

    internal void OpenProject(string projectPath, Guid projectGuid, Guid projectTypeGuid)
    {
      var stringManager = Client.StringManager;
      var id = stringManager.GetId(projectPath);
      var config = ConvertConfig(_config);
      Client.Send(new ClientMessage.ProjectStartLoading(id, projectPath, config));
    }

    private static M.Config ConvertConfig(Ide.Config config)
    {
      var ps = config.ProjectSupport;
      var projectSupport = new M.ProjectSupport(ps.Caption, ps.TypeFullName, ps.Path);
      var languages = config.Languages.Select(x => new M.LanguageInfo(x.Name, x.Path, new M.DynamicExtensionInfo[0])).ToArray();
      var msgConfig = new M.Config(projectSupport, languages, new string[0]);
      return msgConfig;
    }

    internal void BeforeOpenSolution(int id, string solutionPath)
    {
      Client.Send(new ClientMessage.SolutionStartLoading(id, solutionPath));
    }

    internal void AfterOpenSolution(int currentSolutionId)
    {
      Client.Send(new ClientMessage.SolutionLoaded(currentSolutionId));
    }

    internal void AfterOpenProject(int id)
    {
      Client.Send(new ClientMessage.ProjectLoaded(id));
    }

    internal void ReferenceAdded(int id, string path)
    {
      Client.Send(new ClientMessage.ReferenceLoaded(id, path));
    }

    internal void BeforeCloseProject(int id)
    {
      Client.Send(new ClientMessage.ProjectUnloaded(id));
    }

    internal void FileDeleted(int projectId, string path, int id, int version)
    {
      Client.Send(new ClientMessage.FileLoaded(projectId, path, id, version));
    }
  }
}
