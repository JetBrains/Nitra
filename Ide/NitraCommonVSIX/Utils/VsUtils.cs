using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Nitra.ClientServer.Messages;
using Nitra.VisualStudio.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Nitra.VisualStudio
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

      if (ErrorHelper.Succeeded(hierarchy.GetProperty(currentItem, prop, out obj)))
        return (T)obj;

      return default(T);
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

    public static IVsTextBuffer ToIVsTextBuffer(this ITextBuffer textBuffer)
    {
      IVsTextBuffer buffer;

      if (!textBuffer.Properties.TryGetProperty<IVsTextBuffer>(typeof(IVsTextBuffer), out buffer))
        return null;

      return buffer;
    }

    public static readonly Guid GuidIVxTextBuffer = new Guid("be120c41-d969-42a4-a4dd-912665a5bf13");

    public static ITextBuffer ToITextBuffer(this IVsTextBuffer vsTextBuffer)
    {
      object obj2;
      IVsUserData data = vsTextBuffer as IVsUserData;
      if (data == null)
      {
        throw new InvalidOperationException("The shims should allow us to cast to IVsUserData");
      }
      Guid guidIVxTextBuffer = GuidIVxTextBuffer;
      ErrorHelper.ThrowOnFailure(data.GetData(ref guidIVxTextBuffer, out obj2));
      ITextBuffer buffer = obj2 as ITextBuffer;
      if (buffer == null)
      {
        throw new InvalidOperationException("user data doesnt implement the interface");
      }
      return buffer;
    }

    public static readonly Guid GuidIWpfTextViewHost = new Guid("8C40265E-9FDB-4f54-A0FD-EBB72B7D0476");

    public static ITextView ToITextView(this IVsTextView vsTextView)
    {
      return ToIWpfTextView(vsTextView);
    }

    public static IWpfTextView ToIWpfTextView(this IVsTextView vsTextView)
    {
      object obj2;
      IVsUserData data = vsTextView as IVsUserData;
      if (data == null)
      {
        return null;
      }
      Guid guidIWpfTextViewHost = GuidIWpfTextViewHost;
      ErrorHelper.ThrowOnFailure(data.GetData(ref guidIWpfTextViewHost, out obj2));
      IWpfTextViewHost host = obj2 as IWpfTextViewHost;
      return host.TextView;
    }

    public static IVsTextView ToVsTextView(this ITextView view)
    {
      return view.Properties.GetProperty<IVsTextView>(typeof(IVsTextView));
    }

    public static string GetFilePath(this IVsTextView textViewAdapter)
    {
      return GetFilePath(GetBuffer(textViewAdapter));
    }

    public static IVsTextLines GetBuffer(this IVsTextView textViewAdapter)
    {
      IVsTextLines vsTextLines;
      ErrorHelper.ThrowOnFailure(textViewAdapter.GetBuffer(out vsTextLines));
      return vsTextLines;
    }

    public static string GetFilePath(this ITextBuffer textBuffer)
    {
      var vsTextBuffer = (IPersistFileFormat)textBuffer.ToIVsTextBuffer();
      return vsTextBuffer != null ? GetFilePath(vsTextBuffer) : null;
    }

    private static string GetFilePath(IPersistFileFormat persistFileFormat)
    {
      string filePath;
      uint formatIndex;
      ErrorHelper.ThrowOnFailure(persistFileFormat.GetCurFile(out filePath, out formatIndex));
      return filePath;
    }

    public static string GetFilePath(this IVsTextLines vsTextLines)
    {
      return GetFilePath((IPersistFileFormat)vsTextLines);
    }

    public static NSpan Convert(Span span)
    {
      return new NSpan(span.Start, span.End);
    }

    public static Span Convert(NSpan span)
    {
      return new Span(span.StartPos, span.Length);
    }

    public static FileChange Convert(ITextChange change)
    {
      var newLength = change.NewLength;
      var oldLength = change.OldLength;

      if (oldLength == 0 && newLength > 0)
        return new FileChange.Insert(change.OldPosition, change.NewText);
      if (oldLength > 0 && newLength == 0)
        return new FileChange.Delete(Convert(change.OldSpan));

      return new FileChange.Replace(Convert(change.OldSpan), change.NewText);
    }

    public static FileModel GetOrCreateFileModel(IWpfTextView wpfTextView, FileId id, Server server, IVsHierarchy hierarchy, string fullPath)
    {
      var textBuffer = wpfTextView.TextBuffer;
      var props      = textBuffer.Properties;
      FileModel fileModel;
      if (!props.TryGetProperty<FileModel>(Constants.FileModelKey, out fileModel))
        props.AddProperty(Constants.FileModelKey, 
          fileModel = new FileModel(id, textBuffer, server, wpfTextView.VisualElement.Dispatcher, hierarchy, fullPath));
      return fileModel;
    }

    public static TextViewModel GetOrCreateTextViewModel(IWpfTextView wpfTextView, FileModel fileModel)
    {
      TextViewModel textViewModel;
      if (!wpfTextView.Properties.TryGetProperty<TextViewModel>(Constants.TextViewModelKey, out textViewModel))
        wpfTextView.Properties.AddProperty(Constants.TextViewModelKey, textViewModel = fileModel.GetOrAdd(wpfTextView));
      return textViewModel;
    }

    public static FileModel TryGetFileModel(ITextBuffer textBuffer)
    {
      var props = textBuffer.Properties;
      FileModel fileModel;
      if (props.TryGetProperty<FileModel>(Constants.FileModelKey, out fileModel))
        return fileModel;
      return null;
    }

    public static IVsHierarchy GetHierarchyFromVsWindowFrame(this IVsWindowFrame frame)
    {
      object objHier;
      if (ErrorHelper.Succeeded(frame.GetProperty((int)__VSFPROPID.VSFPROPID_Hierarchy, out objHier)))
      {
        var vsHierarchy = (IVsHierarchy)objHier;
        return vsHierarchy;
      }

      return null;
    }

    public static IVsHierarchy GetHierarchyFromCookie(this IVsRunningDocumentTable rdt, uint docCookie)
    {
      uint flags, readlocks, editlocks;
      string name;
      IVsHierarchy hier;
      uint itemid;
      IntPtr docData;

      rdt.GetDocumentInfo(docCookie, out flags, out readlocks, out editlocks, out name, out hier, out itemid, out docData);

      return hier;
    }

    public static FileVersion Convert(this ITextVersion version)
    {
      return new FileVersion(version.VersionNumber - 1);
    }

    public static bool Navigate(this IServiceProvider serviceProvider, string path, int line, int column)
    {
      Guid logicalView = VSConstants.LOGVIEWID_TextView;

      if (VsShellUtilities.ShellIsShuttingDown)
        return false;

      var vsUIShellOpenDocument = (IVsUIShellOpenDocument)serviceProvider.GetService(typeof(IVsUIShellOpenDocument));
      if (vsUIShellOpenDocument == null)
        return false;

      Guid guid = logicalView;
      Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProviderRet;
      IVsUIHierarchy vsUIHierarchy;
      uint pItemId;
      IVsWindowFrame vsWindowFrame;
      if (ErrorHelper.Failed(vsUIShellOpenDocument.OpenDocumentViaProject(path, ref guid, out serviceProviderRet, out vsUIHierarchy, out pItemId, out vsWindowFrame)) || vsWindowFrame == null)
        return false;

      object obj;
      vsWindowFrame.GetProperty(-4004, out obj);
      var vsTextBuffer = obj as VsTextBuffer;
      if (vsTextBuffer == null)
      {
        IVsTextBufferProvider vsTextBufferProvider = obj as IVsTextBufferProvider;
        if (vsTextBufferProvider != null)
        {
          IVsTextLines vsTextLines;
          ErrorHelper.ThrowOnFailure(vsTextBufferProvider.GetTextBuffer(out vsTextLines));
          vsTextBuffer = vsTextLines as VsTextBuffer;
          if (vsTextBuffer == null)
          {
            return false;
          }
        }
      }

      var vsTextManager = (IVsTextManager)serviceProvider.GetService(typeof(VsTextManagerClass));
      if (vsTextManager == null)
        return false;

      if (column > 0)
        column--;

      if (line > 0)
        line--;

      return ErrorHelper.Succeeded(vsTextManager.NavigateToLineAndColumn(vsTextBuffer, ref logicalView, line, column, line, column));
    }

    public static EnvDTE.Project GetProject(this IVsHierarchy hierarchy)
    {
      var itemid = VSConstants.VSITEMID_ROOT;

      var project = hierarchy.GetProp(itemid, __VSHPROPID.VSHPROPID_ExtObject) as EnvDTE.Project;
      return project;
    }

    internal static void NavigateTo(IServiceProvider serviceProvider, string filename, int line, int col)
    {
      IVsTextView viewAdapter;
      IVsWindowFrame pWindowFrame;
      OpenDocument(serviceProvider, filename, out viewAdapter, out pWindowFrame);

      ErrorHandler.ThrowOnFailure(pWindowFrame.Show());

      // Set the cursor at the beginning of the declaration.
      ErrorHandler.ThrowOnFailure(viewAdapter.SetCaretPos(line, col));
      // Make sure that the text is visible.
      viewAdapter.CenterLines(line, 1);
    }

    static void OpenDocument(IServiceProvider serviceProvider,  string filename, out IVsTextView viewAdapter, out IVsWindowFrame pWindowFrame)
    {
      IVsTextManager textMgr = (IVsTextManager)serviceProvider.GetService(typeof(SVsTextManager));

      IVsUIShellOpenDocument uiShellOpenDocument = (IVsUIShellOpenDocument)serviceProvider.GetService(typeof(SVsUIShellOpenDocument));
      IVsUIHierarchy hierarchy;
      uint itemid;


      VsShellUtilities.OpenDocument(
          serviceProvider,
          filename,
          Guid.Empty,
          out hierarchy,
          out itemid,
          out pWindowFrame,
          out viewAdapter);
    }
  }
}
