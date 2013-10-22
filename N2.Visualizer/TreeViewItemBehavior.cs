using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Nitra.Visualizer
{
  /// <summary>
  /// Exposes attached behaviors that can be
  /// applied to TreeViewItem objects.
  /// </summary>
  public static class TreeViewItemBehavior
  {
    #region IsBroughtIntoViewWhenSelected

    public static bool GetIsBroughtIntoViewWhenSelected(TreeViewItem treeViewItem)
    {
      return (bool)treeViewItem.GetValue(IsBroughtIntoViewWhenSelectedProperty);
    }

    public static void SetIsBroughtIntoViewWhenSelected(TreeViewItem treeViewItem, bool value)
    {
      treeViewItem.SetValue(IsBroughtIntoViewWhenSelectedProperty, value);
    }

    public static readonly DependencyProperty IsBroughtIntoViewWhenSelectedProperty =
        DependencyProperty.RegisterAttached(
          "IsBroughtIntoViewWhenSelected",
          typeof(bool),
          typeof(TreeViewItemBehavior),
          new UIPropertyMetadata(false, OnIsBroughtIntoViewWhenSelectedChanged));

    static void OnIsBroughtIntoViewWhenSelectedChanged(
      DependencyObject depObj, DependencyPropertyChangedEventArgs e)
    {
      var item = depObj as TreeViewItem;
      if (item == null)
        return;

      if (e.NewValue is bool == false)
        return;

      if ((bool)e.NewValue)
        item.Selected += OnTreeViewItemSelected;
      else
        item.Selected -= OnTreeViewItemSelected;
    }

    static void OnTreeViewItemSelected(object sender, RoutedEventArgs e)
    {
      // Only react to the Selected event raised by the TreeViewItem
      // whose IsSelected property was modified. Ignore all ancestors
      // who are merely reporting that a descendant's Selected fired.
      if (!Object.ReferenceEquals(sender, e.OriginalSource))
        return;

      var item = e.OriginalSource as TreeViewItem;
      if (item != null)
        item.BringIntoView();
    }

    #endregion // IsBroughtIntoViewWhenSelected
  }
}
