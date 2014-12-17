using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Nitra.Visualizer
{
  public partial class MainWindow
  {
    public static TreeViewItem ObjectToItem(string name, object obj)
    {
      var t = obj.GetType();
      var tvi = new TreeViewItem { Tag = obj, FontWeight = FontWeights.Normal };

      var declatation = obj as IDeclatation;
      if (declatation != null)
      {
        var decl = declatation;
        var props = t.GetProperties();
        var xaml = RenderXamlForDeclaration(name, declatation);
        tvi.Header = XamlReader.Parse(xaml);

        foreach (var prop in props)//.OrderBy(p => p.Name))
        {
          var value = prop.GetValue(declatation);
          tvi.Items.Add(ObjectToItem(prop.Name, value));
        }
      }
      else
      {
        var list = obj as IList;
        if (list != null)
        {
          var items = list;
          var xaml = RenderXamlForlist(name, items);
          tvi.Header = XamlReader.Parse(xaml);
          foreach (var item in items)
            tvi.Items.Add(ObjectToItem("", item));
        }
        else
        {
          var xaml = RenderXamlForValue(name, obj);
          tvi.Header = XamlReader.Parse(xaml);

        }
      }
      return tvi;
    }

    private void UpdateDeclarations(TreeViewItem o)
    {
      using (var d = Dispatcher.DisableProcessing())
        _declarationsTreeView.Items.Add(o);
      _declarationsTreeView.UpdateLayout();
    }

    private static string RenderXamlForDeclaration(string name, IDeclatation declatation)
    {
      return @"
<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
" + (string.IsNullOrWhiteSpace(name) ? null : ("<Span Foreground = 'blue'>" + name + "</Span>: "))
             + declatation.ToXaml() + @"
</Span>";
    }

    private static string RenderXamlForValue(string name, object obj)
    {
      return @"
<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
" + (string.IsNullOrWhiteSpace(name) ? null : ("<Bold>" + name + "</Bold>: "))
             + obj + @"
</Span>";
    }

    private static string RenderXamlForlist(string name, IList items)
    {
      return @"
<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
<Span Foreground = 'blue'>" + name + @"</Span> <Span Foreground = 'gray'>(List) Count: </Span> " + items.Count + @"
</Span>";
    }

    private void _declarationsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
      _propertyGrid.SelectedObject = ((TreeViewItem)e.NewValue).Tag;
    }
  }
}
