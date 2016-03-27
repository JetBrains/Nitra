using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Nitra.Visualizer.Controls
{
  /// <summary>
  /// This class overrides the standard PropertyGrid provided by Microsoft.
  /// It also allows to hide (or filter) the properties of the SelectedObject displayed by the PropertyGrid.
  /// </summary>
  public class PependentPropertyGrid : PropertyGrid
  {
    /// <summary>Contain a reference to the collection of properties to show in the parent PropertyGrid.</summary>
    /// <remarks>By default, m_PropertyDescriptors contain all the properties of the object. </remarks>
    readonly List<PropertyDescriptor> _propertyDescriptors = new List<PropertyDescriptor>();
    /// <summary>Contain a reference to the wrapper that contains the object to be displayed into the PropertyGrid.</summary>
    private ObjectWrapper _wrapper = null;

    /// <summary>Public constructor.</summary>
    public PependentPropertyGrid()
    {
      InitializeComponent();
      base.SelectedObject = _wrapper;
    }

    /// <summary>Overwrite the PropertyGrid.SelectedObject property.</summary>
    /// <remarks>The object passed to the base PropertyGrid is the wrapper.</remarks>
    public new object SelectedObject
    {
      get { return _wrapper != null ? ((ObjectWrapper)base.SelectedObject).SelectedObject : null; }
      set
      {
        // Set the new object to the wrapper and create one if necessary.
        _wrapper = new ObjectWrapper(value);
        RefreshProperties();
        // Set the list of properties to the wrapper.
        _wrapper.PropertyDescriptors = _propertyDescriptors;
        // Link the wrapper to the parent PropertyGrid.
        base.SelectedObject = _wrapper;
      }
    }

    /// <summary>Build the list of the properties to be displayed in the PropertyGrid, following the filters defined the Browsable and Hidden properties.</summary>
    private void RefreshProperties()
    {
      if (_wrapper == null)
        return;
      // Clear the list of properties to be displayed.
      _propertyDescriptors.Clear();

      // Fill the collection with all the properties.
      var obj = _wrapper.SelectedObject;
      if (obj == null)
        return;

      var type = obj.GetType();
      PropertyDescriptorCollection originalPropertyDescriptors = TypeDescriptor.GetProperties(obj);
      foreach (PropertyDescriptor propertyDescriptor in originalPropertyDescriptors)
      {
        var valid = GetValidationProperty(propertyDescriptor, type);

        if (valid == null || (bool)valid.GetValue(obj, null))
          _propertyDescriptors.Add(propertyDescriptor);
        else
          _propertyDescriptors.Add(new EmulatedPropertyDescriptor(propertyDescriptor, valid.Name == "HasValue" ? "<no value>" : "<not evaluated>"));
      }
    }

    private static PropertyInfo GetValidationProperty(PropertyDescriptor propertyDescriptor, Type type)
    {
      var isEvalPropName = "Is" + propertyDescriptor.Name + "Evaluated";
      var isEvalProp = type.GetProperty(isEvalPropName);
      if (isEvalProp != null)
        return isEvalProp;
      if (propertyDescriptor.Name == "Value")
        return type.GetProperty("HasValue");

      return null;
    }

    //********************** Designer

    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Component Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify 
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      components = new System.ComponentModel.Container();
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
    }

    #endregion
  }
}