using System.Reflection;
using System.Windows.Input;
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
    public TreeViewItem ObjectToItem(string name, object obj)
    {
      var tvi = new TreeViewItem { Tag = obj, FontWeight = FontWeights.Normal };
      tvi.MouseDoubleClick += TviOnMouseDoubleClick;
      tvi.KeyDown += TviOnKeyDown;
      tvi.Expanded += TviOnExpanded;

      var list = obj as IAstList<IAst, IDependentPropertyHost>;
      if (list != null)
      {
        var xaml = RenderXamlForlist(name, list);
        tvi.Header = XamlReader.Parse(xaml);
        if (list.Count > 0)
          tvi.Items.Add(obj);
        return tvi;
      }

      var option = obj as IAstOption<IAst, IDependentPropertyHost>;
      if (option != null)
      {
        if (option.HasValue)
          tvi = ObjectToItem(name, option.Value);
        else
        {
          var xaml = RenderXamlForValue(name, "&lt;None&gt;");
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
      if (items != null)
      {
        var type = items.GetType();
        var count = items.Count();
        var xaml = RenderXamlForSeq(name, count);
        tvi.Header = XamlReader.Parse(xaml);
        if (count > 0)
          tvi.Items.Add(obj);
        return tvi;
      }
      else
      {
        var xaml = RenderXamlForValue(name, obj);
        tvi.Header = XamlReader.Parse(xaml);
        return tvi;
      }
    }

    private void TviOnExpanded(object sender, RoutedEventArgs routedEventArgs)
    {
      routedEventArgs.Handled = true;

      var tvi = (TreeViewItem)sender;
      var obj = tvi.Tag;
      tvi.Items.Clear();

      var list = obj as IAstList<IAst, IDependentPropertyHost>;
      if (list != null)
      {
        foreach (var item in list)
          tvi.Items.Add(ObjectToItem("", item));
        return;
      }

      var declaration = obj as IAst;
      if (declaration != null)
      {
        var t = obj.GetType();
        var props = t.GetProperties();

        foreach (var prop in props)//.OrderBy(p => p.Name))
        {
          if (IsIgnoredProperty(prop))
            continue;
          try
          {
            var value = prop.GetValue(declaration, null);
            tvi.Items.Add(ObjectToItem(prop.Name, value));
          }
          catch (Exception e)
          {
            tvi.Items.Add(ObjectToItem(prop.Name, e.Message));
          }
        }
        return;
      }

      var items = obj as IEnumerable;
      if (items != null)
      {
        foreach (var item in (IEnumerable)obj)
          tvi.Items.Add(ObjectToItem("", item));
        return;
      }
    }

    private static bool IsIgnoredProperty(PropertyInfo prop)
    {
      switch (prop.Name)
      {
        case "File":
        case "Span":
        case "IsAmbiguous":
        case "Parent":
          return true;
      }
      return false;
    }

    private void UpdateDeclarations(DeclarationRoot<IAst> declarationRoot)
    {
      var root = ObjectToItem("Root", declarationRoot.Content);
      //using (var d = Dispatcher.DisableProcessing())
      //{
        _declarationsTreeView.Items.Clear();
        _declarationsTreeView.Items.Add(root);
      //  _declarationsTreeView.UpdateLayout();
      //}
    }

    private static string RenderXamlForDeclaration(string name, IAst ast)
    {
      var declatation = ast as IDeclaration;
      var suffix = declatation == null ? null : (": " + declatation.Name);
      return @"
<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
" + (string.IsNullOrWhiteSpace(name) ? null : ("<Span Foreground = 'blue'>" + name + "</Span>: "))
             + ast.ToXaml() + suffix + @"
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

    private static string RenderXamlForlist(string name, IAstList<IAst, IDependentPropertyHost> items)
    {
      return @"
<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
<Span Foreground = 'blue'>" + name + @"</Span> <Span Foreground = 'gray'>(List) Count: </Span> " + items.Count + @"
</Span>";
    }


    private static string RenderXamlForSeq(string name, int count)
    {
      return @"
<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
<Span Foreground = 'blue'>" + name + @"</Span> <Span Foreground = 'gray'>(List) Count: </Span> " + count + @"
</Span>";
    }

    private void _declarationsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
      if (e.NewValue != null)
        _propertyGrid.SelectedObject = ((TreeViewItem)e.NewValue).Tag;
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

      var ast = tvi.Tag as IAst;
      if (ast != null)
      {
        _text.CaretOffset = ast.Span.StartPos;
        _text.Select(ast.Span.StartPos, ast.Span.Length);
        var loc = new Location(_parseResult.OriginalSource, ast.Span);
        _text.ScrollToLine(loc.StartLineColumn.Line);
      }
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
