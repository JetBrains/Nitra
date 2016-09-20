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

    public PropertyAstNodeViewModel(AstContext context, PropertyDescriptor propertyDescriptor)
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

    public override string ToString()
    {
      return _propertyDescriptor.ToString();
    }
  }

  public class ItemAstNodeViewModel : AstNodeViewModel
  {
    public int Index { get; private set; }

    public string Pefix
    {
      get
      {
        if (Index >= 0)
          return "[" + Index + "] ";

        return "";
      }
    }

    public ItemAstNodeViewModel(AstContext context, ObjectDescriptor objectDescriptor, int index) : base(context, objectDescriptor)
    {
      Index = index;
    }

    public override bool IsRoot { get { return Index == -1; } }

    public override string ToString()
    {
      return _objectDescriptor.ToString();
    }
  }

  public abstract class AstNodeViewModel : ReactiveObject
  {
    public class AstContext
    {
      public NitraClient Client      { get; private set; }
      public int         SolutionId  { get; private set; }
      public int         ProjectId   { get; private set; }
      public int         FileId      { get; private set; }
      public int         FileVersion { get; private set; }

      public AstContext(NitraClient client, int solutionId, int projectId, int fileId, int fileVersion)
      {
        Client      = client;
        SolutionId  = solutionId;
        ProjectId   = projectId;
        FileId      = fileId;
        FileVersion = fileVersion;
      }
    }

    readonly protected ObjectDescriptor _objectDescriptor;
    public AstContext Context { get; private set; } 


    [Reactive] public bool                           NeedLoadContent { get; private set; }
               public ReactiveList<AstNodeViewModel> Items           { get; set; }
    [Reactive] public bool                           IsSelected      { get; set; }
    [Reactive] public bool                           IsExpanded      { get; set; }
               public string                         Value           { get { return _objectDescriptor.ToString(); } }
               public NSpan                          Span            { get { return _objectDescriptor.Span; } }
    
    public AstNodeViewModel(AstContext context, ObjectDescriptor objectDescriptor)
    {
      Context           = context;
      _objectDescriptor = objectDescriptor;
      Items = new ReactiveList<AstNodeViewModel>();
      if (objectDescriptor.IsObject || objectDescriptor.IsSeq && objectDescriptor.Count > 0)
      {
        NeedLoadContent = true;
        Items.Add(null);
      }

      this.WhenAnyValue(vm => vm.IsExpanded)
          .Where(isExpanded => isExpanded && NeedLoadContent)
          .Subscribe(_ => LoadItems());
    }

    public virtual bool IsRoot { get { return false; } }

    public void LoadItems()
    {
      if (!NeedLoadContent)
        return;

      NeedLoadContent = false;

      Items.Clear();

      var client = Context.Client;
      client.Send(new ClientMessage.GetObjectContent(Context.SolutionId, Context.FileId, Context.FileVersion, _objectDescriptor.Id));
      var content = client.Receive<ServerMessage.ObjectContent>();

      if (content.content is ContentDescriptor.Fail)
        NeedLoadContent = true;

      _objectDescriptor.SetContent(content.content);

      if (_objectDescriptor.IsObject && _objectDescriptor.Properties != null)
        Items.AddRange(ToProperties(_objectDescriptor.Properties));
      else if (_objectDescriptor.IsSeq && _objectDescriptor.Items != null && _objectDescriptor.Properties != null)
        Items.AddRange(ToAstList(_objectDescriptor.Properties, _objectDescriptor.Items));
      else if (_objectDescriptor.IsSeq && _objectDescriptor.Items != null)
        Items.AddRange(ToItems(_objectDescriptor.Items));
    }

    private IEnumerable<AstNodeViewModel> ToItems(ObjectDescriptor[] objectDescriptors)
    {
      for (int i = 0; i < objectDescriptors.Length; i++)
        yield return new ItemAstNodeViewModel(Context, objectDescriptors[i], i);
    }

    private IEnumerable<AstNodeViewModel> ToAstList(PropertyDescriptor[] propertyDescriptors, ObjectDescriptor[] objectDescriptors)
    {
      foreach (var propertyDescriptor in propertyDescriptors)
        yield return new PropertyAstNodeViewModel(Context, propertyDescriptor);
      for (int i = 0; i < objectDescriptors.Length; i++)
        yield return new ItemAstNodeViewModel(Context, objectDescriptors[i], i);
    }

    private IEnumerable<AstNodeViewModel> ToProperties(PropertyDescriptor[] propertyDescriptors)
    {
      foreach (var propertyDescriptor in propertyDescriptors)
        yield return new PropertyAstNodeViewModel(Context, propertyDescriptor);
    }
  }
}
