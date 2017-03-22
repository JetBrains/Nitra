using Nitra.ClientServer.Messages;
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
}
