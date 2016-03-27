using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace Nitra.Visualizer.Controls
{
  public class EmulatedPropertyDescriptor : PropertyDescriptor
  {
    private readonly PropertyDescriptor _basePropertyDescriptor;
    private readonly string _value;

    public EmulatedPropertyDescriptor(PropertyDescriptor basePropertyDescriptor, string value)
      : base(basePropertyDescriptor)
    {
      _basePropertyDescriptor = basePropertyDescriptor;
      _value = value;
    }

    class NotEvalUITypeEditor : UITypeEditor 
    {
      public override bool GetPaintValueSupported(ITypeDescriptorContext context)
      {
        return true; //Set to true to implement the PaintValue method
      }

      public override void PaintValue(PaintValueEventArgs e)
      {
        using (var brush = new SolidBrush(Color.Red))
          e.Graphics.FillRectangle(brush, e.Bounds);
      }
    }

    public override object GetEditor(Type editorBaseType)
    {
      return new NotEvalUITypeEditor();
    }

    public override bool CanResetValue(object component)
    {
      return false;
    }

    public override object GetValue(object component)
    {
      return _value;
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
