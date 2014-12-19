using Nitra.Declarations;

using System;
using System.Collections;
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
      var tvi = new TreeViewItem { Tag = obj, FontWeight = FontWeights.Normal };

      var list = obj as IDeclarationList<IDeclarationPart>;
      if (list != null)
      {
        var xaml = RenderXamlForlist(name, list);
        tvi.Header = XamlReader.Parse(xaml);
        foreach (var item in list)
          tvi.Items.Add(ObjectToItem("", item));
        return tvi;
      }

      var option = obj as IDeclarationOption<IDeclarationPart>;
      if (option != null)
      {
        if (option.HasValue)
          tvi = ObjectToItem(name, option.Value);
        else
          tvi.Header = XamlReader.Parse(RenderXamlForValue(name, "&lt;None&gt;"));
        return tvi;
      }

      var declaration = obj as IDeclarationPart;
      if (declaration != null)
      {
        var t     = obj.GetType();
        var props = t.GetProperties();
        var xaml  = RenderXamlForDeclaration(name, declaration);
        tvi.Header = XamlReader.Parse(xaml);

        foreach (var prop in props)//.OrderBy(p => p.Name))
        {
          switch (prop.Name)
          {
            case "File":
            case "Span":
            case "IsAmbiguous": 
            case "Parent": continue;

          }

          var value = prop.GetValue(declaration, null);
          tvi.Items.Add(ObjectToItem(prop.Name, value));
        }
        
        return tvi;
      }
      else
      {
        var xaml = RenderXamlForValue(name, obj);
        tvi.Header = XamlReader.Parse(xaml);
        return tvi;
      }
    }

    private void UpdateDeclarations(DeclarationRoot<IDeclarationPart> declarationRoot)
    {
      var root = ObjectToItem("", declarationRoot);
      using (var d = Dispatcher.DisableProcessing())
        _declarationsTreeView.Items.Add(root);
      _declarationsTreeView.UpdateLayout();
    }

    private static string RenderXamlForDeclaration(string name, IDeclarationPart declatation)
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

    private static string RenderXamlForlist(string name, IDeclarationList<IDeclarationPart> items)
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
