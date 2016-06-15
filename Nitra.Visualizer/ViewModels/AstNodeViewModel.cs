using Nitra.ClientServer.Client;
using Nitra.ClientServer.Messages;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Util;

namespace Nitra.Visualizer.ViewModels
{
  public class AstNodeViewModel : ReactiveObject
  {
    public class Context
    {
      public NitraClient Client      { get; private set; }
      public int         FileId      { get; private set; }
      public int         FileVersion { get; private set; }

      public Context(NitraClient client, int fileId, int fileVersion)
      {
        Client      = client;
        FileId      = fileId;
        FileVersion = fileVersion;
      }
    }

    readonly ObjectDescriptor _objectDescriptor;
    readonly Context          _context;

    [Reactive] public bool                           NeedLoadContent { get; private set; }
               public ReactiveList<AstNodeViewModel> Items           { get; set; }
    [Reactive] public bool                           IsSelected      { get; set; }
    [Reactive] public bool                           IsExpanded      { get; set; }
               public string                         Caption         { get { return _objectDescriptor.ToString(); } }

    public IReactiveCommand<IEnumerable<AstNodeViewModel>> LoadItems { get; set; }

    public AstNodeViewModel(Context context, ObjectDescriptor objectDescriptor)
    {
      _context          = context;
      _objectDescriptor = objectDescriptor;
      Items = new ReactiveList<AstNodeViewModel>();
      if (objectDescriptor.IsObject || objectDescriptor.IsSeq && objectDescriptor.Count > 0)
      {
        NeedLoadContent = true;
        Items.Add(null);
      }

      LoadItems = ReactiveCommand.CreateAsyncTask(_ => {
        // load items somehow
        var client = _context.Client;
        client.Send(new ClientMessage.GetObjectContent(_context.FileId, _context.FileVersion, _objectDescriptor.Id));
        var content = client.Receive<ServerMessage.ObjectContent>();
        _objectDescriptor.SetContent(content.content);
        var span = new NSpan(0, _textEditor.Document.TextLength);
        var root = new ObjectDescriptor.Ast(span, 0, members.members);
        return Task.FromResult(Enumerable.Empty<AstNodeViewModel>());
      });

      LoadItems.ObserveOn(RxApp.MainThreadScheduler)
               .Subscribe(items => Items.AddRange(items));

      this.WhenAnyValue(vm => vm.IsExpanded)
          .Where(isExpanded => isExpanded)
          .InvokeCommand(LoadItems);
    }
  }
}
