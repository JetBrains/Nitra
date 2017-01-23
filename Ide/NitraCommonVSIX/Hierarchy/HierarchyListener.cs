using System;
using System.Diagnostics;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System.IO;
using System.ComponentModel;

#pragma warning disable VSSDK002 // Visual Studio service should be used on main thread explicitly.

namespace Nitra.VisualStudio
{
  internal static class HierarchyListenerExtentions
  {
    public static void DisplayProps(this IVsHierarchy hierarchy, uint currentItem, __VSHPROPID prop)
    {
      Debug.WriteLine(">>>" + prop);
      object obj = GetProp(hierarchy, currentItem, prop);

      var extObjectProps = TypeDescriptor.GetProperties(obj);

      foreach (PropertyDescriptor x in extObjectProps)
      {
        var value = x.GetValue(obj);
        Debug.WriteLine("    Name='" + x.Name + "'  Description='" + x.Description + "'  DisplayName='" + x.DisplayName + "'  Value='" + value + "'");
      }
      Debug.WriteLine("<<<" + prop);
    }

    public static object GetProp(this IVsHierarchy hierarchy, uint currentItem, __VSHPROPID prop)
    {
      object obj;
      hierarchy.GetProperty(currentItem, (int)prop, out obj);
      return obj;
    }
  }

  class HierarchyListener : IVsHierarchyEvents, IDisposable
  {
    public IVsHierarchy Hierarchy { get; private set; }
    uint _cookie;

    /// <summary>
    /// Initializes a new instance of the <see cref="HierarchyListener"/> class.
    /// </summary>
    /// <param name="hierarchy">The hierarchy.</param>
    public HierarchyListener(IVsHierarchy hierarchy)
    {
      ErrorHelper.ThrowIsNull(hierarchy, nameof(hierarchy));
      Hierarchy = hierarchy;
    }

    #region Public Methods

    /// <summary>
    /// Gets a value indicating whether this instance is listening.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this instance is listening; otherwise, <c>false</c>.
    /// </value>
    public bool IsListening { get { return (_cookie != 0); } }


    /// <summary>
    /// Starts the listening.
    /// </summary>
    /// <param name="doInitialScan">if set to <c>true</c> [do initial scan].</param>
    public void StartListening(bool doInitialScan)
    {
      if (_cookie == 0)
      {
        ErrorHandler.ThrowOnFailure(Hierarchy.AdviseHierarchyEvents(this, out _cookie));

        if (doInitialScan)
          InternalScanHierarchy(VSConstants.VSITEMID_ROOT);
      }
    }

    /// <summary>
    /// Stops the listening.
    /// </summary>
    public void StopListening()
    {
      InternalStopListening(true);
    }

    #endregion

    #region IDisposable Members

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
      InternalStopListening(false);

      _cookie = 0;
      Hierarchy = null;
    }

    #endregion

    #region Public Events

    public event EventHandler<HierarchyItemEventArgs> ItemAdded;
    public event EventHandler<HierarchyItemEventArgs> ItemDeleted;

    public event EventHandler<ReferenceEventArgs> ReferenceAdded;

    #endregion

    #region IVsHierarchyEvents Members

    public int OnInvalidateIcon(IntPtr hicon)
    {
      // Do Nothing.
      //
      return VSConstants.S_OK;
    }

    public int OnInvalidateItems(uint itemidParent)
    {
      // TODO: Find out if this event is needed.
      //
      Debug.WriteLine("\n\tOnInvalidateItems\n");
      return VSConstants.S_OK;
    }

    public int OnItemAdded(uint itemidParent, uint itemidSiblingPrev, uint itemidAdded)
    {
      // Check if the item is a nemerle file.
      //

#if DEBUG
      string itemName;
      string parentName;
      string siblingName;

      Hierarchy.GetCanonicalName(itemidAdded, out itemName);
      Hierarchy.GetCanonicalName(itemidParent, out parentName);
      Hierarchy.GetCanonicalName(itemidSiblingPrev, out siblingName);

      Debug.WriteLine(string.Format("tr: \tOnItemsAdded '{0}' to '{1}' next to '{2}'", Path.GetFileName(itemName),
      Path.GetFileName(parentName),
      Path.GetFileName(siblingName)));
#endif

      if (TryFierOnAddReference(itemidAdded))
        return VSConstants.S_OK;

      string name;

      if (!IsFile(itemidAdded, out name))
        return VSConstants.S_OK;

      // This item is a physical file, so we can notify that it is added to the hierarchy.
      //
      if (ItemAdded != null)
      {
        HierarchyItemEventArgs args = new HierarchyItemEventArgs(Hierarchy, itemidAdded, name);
        ItemAdded(this, args);
      }

      return VSConstants.S_OK;
    }

    private bool TryFierOnAddReference(uint currentItem)
    {
      if (ReferenceAdded == null)
        return false;

      int hr2;
      object obj;
      hr2 = Hierarchy.GetProperty(currentItem, (int)__VSHPROPID.VSHPROPID_ExtObject, out obj);

      if (!ErrorHelper.Succeeded(hr2))
        return false;

      var reference = obj as VSLangProj.Reference;
      var projectItem = obj as EnvDTE.ProjectItem;

      if (reference == null && projectItem != null)
        reference = projectItem.Object as VSLangProj.Reference;

      if (reference != null)
      {
        ReferenceAdded(this, new ReferenceEventArgs(Hierarchy, currentItem, reference));
        return true;
      }

      return false;
    }

    public int OnItemDeleted(uint itemid)
    {
      Debug.WriteLine("tr: \tOnItemDeleted");

      // Notify that the item is deleted only if it is a nemerle file.
      //
      string name;

      if (!IsFile(itemid, out name))
        return VSConstants.S_OK;

      if (ItemDeleted != null)
      {
        HierarchyItemEventArgs args = new HierarchyItemEventArgs(Hierarchy, itemid, name);
        ItemDeleted(this, args);
      }

      return VSConstants.S_OK;
    }

    public int OnItemsAppended(uint itemidParent)
    {
      // TODO: Find out what this event is about.
      //
      string name;
      Hierarchy.GetCanonicalName(itemidParent, out name);
      Debug.WriteLine(string.Format("tr: \tOnItemsAppended: '{0}'", Path.GetFileName(name)));

      return VSConstants.S_OK;
    }

    public int OnPropertyChanged(uint itemid, int propid, uint flags)
    {
      // Do Nothing.
      //
      return VSConstants.S_OK;
    }

    #endregion

    bool InternalStopListening(bool throwOnError)
    {
      if (null == Hierarchy || 0 == _cookie)
        return false;

      int hr = Hierarchy.UnadviseHierarchyEvents(_cookie);

      if (throwOnError)
        ErrorHandler.ThrowOnFailure(hr);

      _cookie = 0;

      return ErrorHandler.Succeeded(hr);
    }

    bool IsFile(uint itemId, out string canonicalName)
    {
      canonicalName = null;

      // Find out if this item is a physical file.
      //
      Guid typeGuid;
      int hr = Hierarchy.GetGuidProperty(itemId, (int)__VSHPROPID.VSHPROPID_TypeGuid, out typeGuid);

      if (ErrorHandler.Failed(hr) || VSConstants.GUID_ItemType_PhysicalFile != typeGuid)
        // It is not a file, we can exit now.
        return false;

      // This item is a file; find if it is a nemerle file.
      //
      hr = Hierarchy.GetCanonicalName(itemId, out canonicalName);

      if (ErrorHandler.Failed(hr))
        return false;

      return true;
    }

    /// <summary>
    /// Do a recursive walk on the hierarchy to find all the nemerle files in
    /// it. It will generate an event for every file found.
    /// </summary>
    void InternalScanHierarchy(uint itemId)
    {
      uint currentItem = itemId;

      while (VSConstants.VSITEMID_NIL != currentItem)
      {
        // If this item is a nemerle file, then send the add item event.
        string itemName;

        if (ItemAdded != null && IsFile(currentItem, out itemName))
          ItemAdded(this, new HierarchyItemEventArgs(Hierarchy, currentItem, itemName));
        else
          TryFierOnAddReference(currentItem);

        // NOTE: At the moment we skip the nested hierarchies, so here  we 
        // look for the  children of this node. Before looking at the children
        // we have to make sure that the enumeration has not side effects to 
        // avoid unexpected behavior.
        //
        object propertyValue;
        bool canScanSubitems = true;
        int hr = Hierarchy.GetProperty(currentItem, (int)__VSHPROPID.VSHPROPID_HasEnumerationSideEffects, out propertyValue);

        if ((VSConstants.S_OK == hr) && (propertyValue is bool))
          canScanSubitems = !(bool)propertyValue;

        // If it is allow to look at the sub-items of the current one, lets do it.
        //
        if (canScanSubitems)
        {
          object child;

          hr = Hierarchy.GetProperty(currentItem, (int)__VSHPROPID.VSHPROPID_FirstChild, out child);

          if (VSConstants.S_OK == hr)
            // There is a sub-item, call this same function on it.
            InternalScanHierarchy(GetItemId(child));
        }

        // Move the current item to its first visible sibling.
        //
        object sibling;

        hr = Hierarchy.GetProperty(currentItem, (int)__VSHPROPID.VSHPROPID_NextSibling, out sibling);

        currentItem = VSConstants.S_OK != hr ? VSConstants.VSITEMID_NIL : GetItemId(sibling);
      }
    }

    /// <summary>
    /// Gets the item id.
    /// </summary>
    /// <param name="variantValue">VARIANT holding an itemid.</param>
    /// <returns>Item Id of the concerned node</returns>
    static uint GetItemId(object variantValue)
    {
      if (variantValue == null) return VSConstants.VSITEMID_NIL;

      if (variantValue is int) return (uint)(int)variantValue;
      if (variantValue is uint) return (uint)variantValue;
      if (variantValue is short) return (uint)(short)variantValue;
      if (variantValue is ushort) return (ushort)variantValue;
      if (variantValue is long) return (uint)(long)variantValue;

      return VSConstants.VSITEMID_NIL;
    }
  }
}