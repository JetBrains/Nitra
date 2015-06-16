using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace Nitra.Visualizer.Controls
{
  public class EmulatedPropertyDescriptor : PropertyDescriptor
  {
    private readonly PropertyDescriptor _basePropertyDescriptor;

    public EmulatedPropertyDescriptor(PropertyDescriptor basePropertyDescriptor) : base(basePropertyDescriptor)
    {
      _basePropertyDescriptor = basePropertyDescriptor;
    }

    public override object GetEditor(Type editorBaseType)
    {
      return null;
    }

    public override bool CanResetValue(object component)
    {
      return false;
    }

    public override object GetValue(object component)
    {
      return "<not evaluated>";
    }

    public override void ResetValue(object component)
    {
    }

    public override void SetValue(object component, object value)
    {
    }

    public override bool ShouldSerializeValue(object component)
    {
      return false;
    }

    public override Type ComponentType
    {
      get { return _basePropertyDescriptor.ComponentType; }
    }

    public override bool IsReadOnly
    {
      get { return true; }
    }

    public override Type PropertyType
    {
      get { return _basePropertyDescriptor.PropertyType; }
    }
  }
}
