using Nitra.Declarations;

using System.Reflection;
using System.Windows.Input;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Nitra.Internal;
using Nitra.ProjectSystem;
using Nitra.Runtime.Reflection;

namespace Nitra.Visualizer
{
  public partial class MainWindow
  {
    private IAst _astRoot;

    private TreeViewItem ObjectToItem(PropertyInfo prop, object obj)
    {
      string name = prop == null ? "" : prop.Name;
      var tvi = new TreeViewItem { Tag = obj, FontWeight = FontWeights.Normal };
      tvi.MouseDoubleClick += TviOnMouseDoubleClick;
      tvi.KeyDown += TviOnKeyDown;
      tvi.Expanded += TviOnExpanded;

      var list = obj as IAstList<IAst>;
      if (list != null)
      {
        var xaml = RenderXamlForlist(name, list);
        tvi.Header = XamlReader.Parse(xaml);
        if (list.Count > 0)
          tvi.Items.Add(obj);
        return tvi;
      }

      var option = obj as IAstOption<IAst>;
      if (option != null)
      {
        if (option.HasValue)
          tvi = ObjectToItem(prop, option.Value);
        else
        {
          var xaml = RenderXamlForValue(prop, "<None>");
          tvi.Header = XamlReader.Parse(xaml);
        }
        return tvi;
      }

      var declaration = obj as IAst;
      if (declaration != null)
      {
        var xaml   = RenderXamlForDeclaration(name, declaration);
        tvi.Header = XamlReader.Parse(xaml);
        var t      = obj.GetType();
        var props  = t.GetProperties();

        if (props.Any(p => !IsIgnoredProperty(p)))
          tvi.Items.Add(obj);
        
        return tvi;
      }

      var items = obj as IEnumerable;
      if (items != null && !(items is string))
      {
        var type = items.GetType();
        var count = items.Count();
        var xaml = RenderXamlForSeq(name, items);
        tvi.Header = XamlReader.Parse(xaml);
        if (count > 0)
          tvi.Items.Add(obj);
        return tvi;
      }
      else
      {
        var xaml = RenderXamlForValue(prop, obj);
        tvi.Header = XamlReader.Parse(xaml);

        if (obj == null)
          return tvi;

        var t = obj.GetType();
        var props = t.GetProperties();
        if (!(obj is string || t.IsPrimitive) && props.Any(p => !IsIgnoredProperty(p)))
          tvi.Items.Add(obj);

        return tvi;
      }
    }

    private void TviOnExpanded(object sender, RoutedEventArgs routedEventArgs)
    {
      routedEventArgs.Handled = true;

      var tvi = (TreeViewItem)sender;
      TviExpanded(tvi);
    }

    private void TviExpanded(TreeViewItem tvi)
    {
      var obj = tvi.Tag;
      tvi.Items.Clear();

      var list = obj as IAstList<IAst>;
      if (list != null)
      {
        foreach (var item in list)
          tvi.Items.Add(ObjectToItem(null, item));
        return;
      }

      if (obj is IAstOption<IAst>)
        return;

      var declaration = obj as IAst;
      if (declaration != null)
      {
        var t = obj.GetType();
        var props = t.GetProperties();

        foreach (var prop in props.OrderBy(p => p.Name))
        {
          if (IsIgnoredProperty(prop))
            continue;
          try
          {
            if (declaration.IsMissing)
              return;

            ReadValue(tvi, declaration, t, prop);
          }
          catch (Exception e)
          {
            tvi.Items.Add(ObjectToItem(prop, e.Message));
          }
        }
        return;
      }

      var items = obj as IEnumerable;
      if (items != null && !(items is string))
      {
        foreach (var item in (IEnumerable) obj)
          tvi.Items.Add(ObjectToItem(null, item));
        return;
      }

      {
        var t = obj.GetType();

        if (obj is string || t.IsPrimitive)
          return;

        var props = t.GetProperties();

        foreach (var prop in props.OrderBy(p => p.Name))
        {
          if (IsIgnoredProperty(prop))
            continue;
          try
          {
            ReadValue(tvi, obj, t, prop);
          }
          catch (Exception e)
          {
            tvi.Items.Add(ObjectToItem(prop, e.Message));
          }
        }
      }
    }

    private void ReadValue(TreeViewItem tvi, object obj, System.Type t, PropertyInfo prop)
    {
      var isEvalPropName = "Is" + prop.Name + "Evaluated";
      var isEvalProp = t.GetProperty(isEvalPropName);
      if (isEvalProp == null || (bool)isEvalProp.GetValue(obj, null))
      {
        var value = prop.GetValue(obj, null);
        tvi.Items.Add(ObjectToItem(prop, value));
      }
      else
      {
        var tviNotEval = ObjectToItem(prop, "<not evaluated>");
        tviNotEval.Foreground = Brushes.Red;
        tviNotEval.FontWeight = FontWeights.Bold;
        tvi.Items.Add(tviNotEval);
      }
    }

    private static bool IsIgnoredProperty(PropertyInfo prop)
    {
      var name = prop.Name;
      switch (prop.Name)
      {
        case "HasValue":
          return false;
        case "Id":
        case "IsMissing":
        case "File":
        case "Span":
        case "IsAmbiguous":
          return true;
        default:
          if (name.StartsWith("Is", StringComparison.Ordinal) && name.EndsWith("Evaluated", StringComparison.Ordinal) && !name.Equals("IsAllPropertiesEvaluated", StringComparison.Ordinal))
            return true;

          return false;
      }
    }

    private void UpdateDeclarations()
    {
      if (_astRoot == null)
        return;

      _declarationsTreeView.Items.Clear();

      var rootTreeViewItem = ObjectToItem(null, _astRoot);
      rootTreeViewItem.Header = "Root";
      _declarationsTreeView.Items.Add(rootTreeViewItem);
    }

    private static string RenderXamlForDeclaration(string name, IAst ast)
    {
      var declatation = ast as Declaration;
      var suffix = declatation == null ? null : (": " + Utils.Escape(declatation.Name.Text));
      return @"
<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
" + (string.IsNullOrWhiteSpace(name) ? null : ("<Span Foreground = 'blue'>" + Utils.Escape(name) + "</Span>: "))
             + ast.ToXaml() + suffix + @"
</Span>";
    }

    private static string RenderXamlForValue(PropertyInfo prop, object obj)
    {
      if (obj == null)
        obj = "<null>";
      var header = "";
      var tooltip = "";
      if (prop != null)
      {
        var color = "SlateBlue";
        var prefix = "";
        
        var attr = (PropertyAttribute)prop.GetCustomAttributes(typeof(PropertyAttribute), false).FirstOrDefault();
        if (attr != null && attr.IsDependent)
        {
          color = "green";
          prefix = attr.IsOut
            ? "<Span Foreground='blue'>out</Span> "
            : "<Span Foreground='blue'>in</Span> ";
          tooltip = "ToolTip='" + Utils.Escape(attr.FullName) + "'";
        }
        header = prefix + "<Bold><Span Foreground='" + color + "'>" + Utils.Escape(prop.Name) + "</Span></Bold>: ";
      }
      return "<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " + tooltip + ">" + header + Utils.Escape(obj.ToString()) + "</Span>";
    }

    private static string RenderXamlForlist(string name, IAstList<IAst> items)
    {
      return @"
<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
<Span Foreground = 'blue'>" + Utils.Escape(name) + @"</Span>* <Span Foreground = 'gray'>Count: </Span> " + items.Count + @"
</Span>";
    }

    private static string RenderXamlForSeq(string name, IEnumerable items)
    {
        var count = items.Count();
        var itemsString = items.ToString()
                               .Replace("<", "&lt;")
                               .Replace(">", "&gt;");
      return @"
<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
<Span Foreground = 'green'>" + Utils.Escape(name) + @"</Span> <Span Foreground = 'gray'>(Count: " + count + ") </Span> " + itemsString + @"
</Span>";
    }

    private void _declarationsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
      if (e.NewValue != null)
      {
        var obj = ((TreeViewItem) e.NewValue).Tag;
        var symbol = obj as DeclarationSymbol;
        var id = symbol != null ? symbol.Id : (obj == null ? 0 : obj.GetHashCode());
        try
        {
          _propertyGrid.SelectedObject = obj;
        }
        catch
        {
        }
        _objectType.Text = obj == null ? "<null>" : obj.GetType().FullName + " [" + id + "]";
      }
    }

    private void TviOnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      SelectCodeForDeclarationPart(sender);
      e.Handled = true;
    }

    private void SelectCodeForDeclarationPart(object sender)
    {
      var tvi = (TreeViewItem) sender;
      if (!tvi.IsSelected)
        return;

      var loc = tvi.Tag as ILocated;
      if (loc != null)
      {
        SelectText(loc);
        return;
      }

      TrySelectTextForSymbol(tvi.Tag as DeclarationSymbol, tvi);
    }

    private void TrySelectTextForSymbol(DeclarationSymbol symbol, TreeViewItem tvi)
    {
      if (symbol != null)
      {
        var declarations = symbol.GetDeclarationsUntyped().ToList();
        if (declarations.Count == 1)
          SelectText(declarations[0]);
        else if (declarations.Count > 1)
        {
          if (!tvi.IsExpanded)
            tvi.IsExpanded = true;
          foreach (TreeViewItem subItem in tvi.Items)
          {
            var decls = subItem.Tag as IEnumerable<Declaration>;
            if (decls != null)
            {
              subItem.IsExpanded = true;
              subItem.IsSelected = true;

              foreach (TreeViewItem subSubItem in tvi.Items)
                subSubItem.BringIntoView();

              break;
            }
          }
        }
      }
    }

    private void SelectText(ILocated loc)
    {
      SelectText(loc.File, loc.Span);
    }

    private void SelectText(Location loc)
    {
      SelectText(loc.Source.File, loc.Span);
    }

    private void SelectText(File file, NSpan span)
    {
      if (_currentTestFolder != null)
      {
        foreach (var test in _currentTestFolder.Tests)
        {
          if (test.File == file)
          {
            test.IsSelected = true;
            break;
          }
        }
      }
      _text.CaretOffset = span.StartPos;
      _text.Select(span.StartPos, span.Length);
      _text.ScrollTo(_text.TextArea.Caret.Line, _text.TextArea.Caret.Column);
    }

    private void TviOnKeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Return)
      {
        SelectCodeForDeclarationPart(sender);
        e.Handled = true;
      }
    }
  }
}
