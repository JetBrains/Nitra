using Microsoft.VisualStudio.Language.Intellisense;

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Nitra.VisualStudio.QuickInfo
{
  class PopupContainer : Grid, IInteractiveQuickInfoContent
  {
    public bool             IsMouseOverAggregated { get; set; }
    public bool             KeepQuickInfoOpen     { get; set; }
    public FrameworkElement RootElementOpt        { get; private set; }
    public Popup            PopupOpt              { get; private set; }

    public event EventHandler PopupOpened;

    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
      var root = FindRoot(this);
      if (ReferenceEquals(root, this)) // no parent
      {
        PopupOpt.Opened           -= Popup_Opened;
        RootElementOpt = null;
        PopupOpt = null;
        return;
      }
      //var ancestors = new List<DependencyObject>();
      //FindAncestors(this, ancestors);

      var popup = (Popup)root.Parent;

      PopupOpt       = popup;
      RootElementOpt = root;
      popup.Opened   += Popup_Opened;

      base.OnVisualParentChanged(oldParent);
    }

    void Popup_Opened(object sender, EventArgs e)
    {
      PopupOpened?.Invoke(sender, e);
    }

    public static FrameworkElement FindRoot(DependencyObject dependencyObject)
    {
      var parent = VisualTreeHelper.GetParent(dependencyObject);

      if (parent == null)
        return (FrameworkElement)dependencyObject;

      return FindRoot(parent);
    }

    public static void FindAncestors(DependencyObject dependencyObject, List<DependencyObject> path)
    {
      if (dependencyObject == null)
        return;

      path.Add(dependencyObject);

      var parent = VisualTreeHelper.GetParent(dependencyObject);

      if (parent == null)
        return;

      FindAncestors(parent, path);
    }
  }
}
