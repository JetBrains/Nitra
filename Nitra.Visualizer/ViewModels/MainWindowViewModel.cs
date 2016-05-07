using Nitra.ClientServer.Messages;
using Nitra.ViewModels;
using Nitra.Visualizer.Properties;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Nitra.Visualizer.ViewModels
{
  public class MainWindowViewModel : ReactiveObject
  {
    public WorkspaceVm Workspace { get; set; }
    public SuiteVm CurrentSuite { get; set; }
    public ProjectVm CurrentProject { get; set; }
    public TestVm CurrentTest { get; set; }
    public SolutionVm CurrentSolution { get; set; }
    public Settings Settings { get; private set; }

    public NitraTextEditorViewModel Editor { get; private set; }

    [Reactive] public string StatusText { get; private set; }

    public MainWindowViewModel()
    {
      Editor = new NitraTextEditorViewModel(this);

      Settings = Settings.Default;
    }

    public void FindSymbolDefinitions()
    {
      if (CurrentSuite == null || CurrentTest == null)
        return;

      var client = CurrentSuite.Client;
      var pos = Editor.CaretOffset;

      client.Send(new ClientMessage.FindSymbolDefinitions(CurrentTest.Id, CurrentTest.Version, pos));

      var result = client.Receive<ServerMessage.FindSymbolDefinitions>();

      if (result.definitions.Length == 0)
        StatusText = "No symbols found!";
      else if (result.definitions.Length == 1)
        Editor.SelectText(result.definitions[0].Location);
      else
      {
        Editor.PopupList.Clear();

        foreach (var d in result.definitions)
        {
          var f = d.Location.File;
          var file = CurrentSolution.GetFile(f.FileId);
          if (file.Version != f.FileVersion)
            continue;

          Editor.PopupList.Add(new PopupItemViewModel(file.Name, d.Location.Span, d, Editor));
        }

        Editor.PopupVisible = true;
      }
    }
  }
}