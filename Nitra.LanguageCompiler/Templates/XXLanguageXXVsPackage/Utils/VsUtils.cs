using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace XXNamespaceXX
{
  internal static class VsUtils
  {
    public static string GetAssemblyPath(Assembly assembly)
    {
      var codeBase = assembly.CodeBase;
      var uri      = new UriBuilder(codeBase);
      var path     = Uri.UnescapeDataString(uri.Path);
      return path;
    }

    public static string GetPlaginPath()
    {
      return Path.GetDirectoryName(GetAssemblyPath(Assembly.GetExecutingAssembly()));
    }

    public static string GetFilePath(this IVsWindowFrame pFrame)
    {
      ThreadHelper.ThrowIfNotOnUIThread();
      object data;
      pFrame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out data);
      return (string)data;
    }

    public static T GetProp<T>(IVsHierarchy hierarchy, uint currentItem, int prop)
    {
      object obj;
      ThreadHelper.ThrowIfNotOnUIThread();
      ErrorHelper.ThrowOnFailure(hierarchy.GetProperty(currentItem, prop, out obj));
      return (T)obj;
    }

    public static T GetProp<T>(this IVsHierarchy hierarchy, uint currentItem, __VSHPROPID prop)
    {
      return GetProp<T>(hierarchy, currentItem, (int)prop);
    }

    public static T GetProp<T>(this IVsHierarchy hierarchy, uint currentItem, __VSHPROPID2 prop)
    {
      return GetProp<T>(hierarchy, currentItem, (int)prop);
    }

    public static T GetProp<T>(this IVsHierarchy hierarchy, uint currentItem, __VSHPROPID3 prop)
    {
      return GetProp<T>(hierarchy, currentItem, (int)prop);
    }

    public static T GetProp<T>(this IVsHierarchy hierarchy, uint currentItem, __VSHPROPID4 prop)
    {
      return GetProp<T>(hierarchy, currentItem, (int)prop);
    }

    public static T GetProp<T>(this IVsHierarchy hierarchy, uint currentItem, __VSHPROPID5 prop)
    {
      return GetProp<T>(hierarchy, currentItem, (int)prop);
    }
  }
}
