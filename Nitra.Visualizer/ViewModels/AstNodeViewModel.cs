using Nitra.ClientServer.Client;
using Nitra.ClientServer.Messages;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nitra.Visualizer.ViewModels
{
  public class AstNodeViewModel : ReactiveObject
  {
    readonly ObjectDescriptor _objectDescriptor;
    readonly NitraClient _client;

    [Reactive] public bool                           NeedLoadContent { get; private set; }
               public ReactiveList<AstNodeViewModel> Items           { get; set; }
    [Reactive] public bool                           IsSelected      { get; set; }
    [Reactive] public bool                           IsExpanded      { get; set; }

    public AstNodeViewModel(NitraClient client, ObjectDescriptor objectDescriptor)
    {
      _client           = client;
      _objectDescriptor = objectDescriptor;
      Items = new ReactiveList<AstNodeViewModel>();
      if (objectDescriptor.IsObject || objectDescriptor.IsSeq && objectDescriptor.Count > 0)
      {
        NeedLoadContent = true;
        Items.Add(null);
      }

      this.WhenAnyValue(vm => vm.IsExpanded)
        .Where(isExpanded => isExpanded)
        .InvokeCommand(OnLoadItems);
    }

    private void OnLoadItems(object x)
    {
    }
  }
}
