using Nitra.ClientServer.Messages;
using Nitra.ViewModels;
using Nitra.Visualizer.Properties;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Nitra.Visualizer.ViewModels
{
  public class MainWindowViewModel : ReactiveObject
  {
    [Reactive] public WorkspaceVm Workspace { get; set; }
    [Reactive] public SuiteVm CurrentSuite { get; set; }
    [Reactive] public ProjectVm CurrentProject { get; set; }
    [Reactive] public TestVm CurrentTest { get; set; }
    [Reactive] public SolutionVm CurrentSolution { get; set; }
    [Reactive] public Settings Settings { get; private set; }
    [Reactive] public string StatusText { get; private set; }
    
    public NitraTextEditorViewModel Editor { get; private set; }

    public IReactiveCommand<object> FindSymbolDefinitions { get; private set; }
    public IReactiveCommand<object> FindSymbolReferences  { get; private set; }

    public MainWindowViewModel()
    {
      Editor = new NitraTextEditorViewModel(this);
      Settings = Settings.Default;

      var canFindSymbolDefinitions = this.WhenAny(v => v.CurrentSuite, v => v.CurrentTest, 
                                                  (suite, test) => suite != null && test != null);

      FindSymbolDefinitions = ReactiveCommand.Create(canFindSymbolDefinitions);
      FindSymbolDefinitions.ThrownExceptions.Subscribe(e => 
        StatusText = "GOTO definition failed!");
      FindSymbolDefinitions.Subscribe(OnFindSymbolDefinitions);

      FindSymbolReferences = ReactiveCommand.Create(canFindSymbolDefinitions);
      FindSymbolReferences.ThrownExceptions.Subscribe(e => 
        StatusText = "Find all references definition failed!");
      FindSymbolReferences.Subscribe(OnFindSymbolReferences);
    }

    private void OnFindSymbolReferences(object _)
    {
      var client = CurrentSuite.Client;
      var pos = Editor.CaretOffset;
      client.Send(new ClientMessage.FindSymbolReferences(CurrentTest.Id, CurrentTest.Version, pos));
      var msg = client.Receive<ServerMessage.FindSymbolReferences>();
      Debug.WriteLine("OnFindSymbolReferences()");
    }

    private void OnFindSymbolDefinitions(object _)
    {
      var client = CurrentSuite.Client;
      var pos = Editor.CaretOffset;

      client.Send(new ClientMessage.FindSymbolDefinitions(CurrentTest.Id, CurrentTest.Version, pos));

      var msg = client.Receive<ServerMessage.FindSymbolDefinitions>();

      if (msg.definitions.Length == 0)
        StatusText = "No symbols found!";
      else if (msg.definitions.Length == 1)
        Editor.SelectText(msg.definitions[0].Location);
      else {
        Editor.IntelliSensePopup.Items.Clear();

        foreach (var definition in msg.definitions.OrderBy(d => d.Location.Span.StartPos)) {
          var f = definition.Location.File;
          var file = CurrentSolution.GetFile(f.FileId);
          if (file.Version != f.FileVersion)
            continue;

          Editor.IntelliSensePopup.Items.Add(new PopupItemViewModel(file.Name, definition.Location.Span, definition, Editor.IntelliSensePopup));
        }

        Editor.IntelliSensePopup.IsVisible = true;
      }
    }
  }
}