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
using System.Windows.Media;

namespace Nitra.Visualizer.ViewModels
{
  public class PropertyAstNodeViewModel : AstNodeViewModel
  {
    readonly PropertyDescriptor _propertyDescriptor;

    public PropertyAstNodeViewModel(Context context, PropertyDescriptor propertyDescriptor)
      : base(context, propertyDescriptor.Object)
    {
      _propertyDescriptor = propertyDescriptor;
    }

    public string Name
    {
      get { return _propertyDescriptor.Name; }
    }

    public string Pefix
    {
      get
      {
        switch (_propertyDescriptor.Kind)
        {
          case PropertyKind.Ast:            return "ast ";
          case PropertyKind.DependentIn:    return "in ";
          case PropertyKind.DependentInOut: return "inout ";
          case PropertyKind.DependentOut:   return "out ";
          default:                          return "";
        }
      }
    }

    public Brush Foreground
    {
      get
      {
        switch (_propertyDescriptor.Kind)
        {
          case PropertyKind.Ast:            return Brushes.DarkGoldenrod;
          case PropertyKind.DependentIn:
          case PropertyKind.DependentInOut:
          case PropertyKind.DependentOut:   return Brushes.Green;
          default:                          return Brushes.DarkGray;
        }
      }
    }
  }

  public class ItemAstNodeViewModel : AstNodeViewModel
  {
    public ItemAstNodeViewModel(Context context, ObjectDescriptor objectDescriptor) : base(context, objectDescriptor) { }
  }

  public abstract class AstNodeViewModel : ReactiveObject
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

    readonly protected ObjectDescriptor _objectDescriptor;
    readonly protected Context          _context;

    [Reactive] public bool                           NeedLoadContent { get; private set; }
               public ReactiveList<AstNodeViewModel> Items           { get; set; }
    [Reactive] public bool                           IsSelected      { get; set; }
    [Reactive] public bool                           IsExpanded      { get; set; }
               public string                         Value           { get { return _objectDescriptor.ToString(); } }

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
        NeedLoadContent = false;
        var client = _context.Client;
        client.Send(new ClientMessage.GetObjectContent(_context.FileId, _context.FileVersion, _objectDescriptor.Id));
        var content = client.Receive<ServerMessage.ObjectContent>();
        _objectDescriptor.SetContent(content.content);

        if (_objectDescriptor.IsObject && _objectDescriptor.Properties != null)
          return Task.FromResult(ToProperties(_objectDescriptor.Properties));
        else if (_objectDescriptor.IsSeq && _objectDescriptor.Items != null)
          return Task.FromResult(ToItems(_objectDescriptor.Items));
        else
          return Task.FromResult(Enumerable.Empty<AstNodeViewModel>());
      });

      LoadItems.ObserveOn(RxApp.MainThreadScheduler)
        .Do(_ => Items.Clear())
        .Subscribe(items => Items.AddRange(items));

      this.WhenAnyValue(vm => vm.IsExpanded)
          .Where(isExpanded => isExpanded && NeedLoadContent)
          .InvokeCommand(LoadItems);
    }

    private IEnumerable<AstNodeViewModel> ToItems(ObjectDescriptor[] objectDescriptors)
    {
      foreach (var objectDescriptor in objectDescriptors)
        yield return new ItemAstNodeViewModel(_context, objectDescriptor);
    }

    private IEnumerable<AstNodeViewModel> ToProperties(PropertyDescriptor[] propertyDescriptors)
    {
      foreach (var propertyDescriptor in propertyDescriptors)
        yield return new PropertyAstNodeViewModel(_context, propertyDescriptor);
    }
  }
}
