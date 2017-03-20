using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
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
using System.Windows;
using System.Windows.Media;

using D = System.Drawing;

namespace Nitra.VisualStudio
{
  internal static class VsUtils
  {
    public static D.Rectangle ToRectangle(this Rect r) => new D.Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);

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

    public static FileModel GetOrCreateFileModel(IWpfTextView wpfTextView, FileId id, ServerModel server, IVsHierarchy hierarchy, string fullPath)
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

    public static TextViewModel TryGetTextViewModel(this ITextView wpfTextView)
    {
      TextViewModel textViewModel;
      wpfTextView.Properties.TryGetProperty(Constants.TextViewModelKey, out textViewModel);
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

    public static EnvDTE.Project GetProject(this IVsHierarchy hierarchy)
    {
      var itemid = VSConstants.VSITEMID_ROOT;

      var project = hierarchy.GetProp(itemid, __VSHPROPID.VSHPROPID_ExtObject) as EnvDTE.Project;
      return project;
    }

    internal static void NavigateTo(IServiceProvider serviceProvider, string filename, int pos)
    {
      IVsTextView viewAdapter;
      IVsWindowFrame pWindowFrame;
      OpenDocument(serviceProvider, filename, out viewAdapter, out pWindowFrame);
      if (pWindowFrame == null || viewAdapter == null)
        return;
      ErrorHandler.ThrowOnFailure(pWindowFrame.Show());

      var wpfTextView = viewAdapter.ToIWpfTextView();
      var textViewModel = wpfTextView.TryGetTextViewModel();
      if (textViewModel == null)
        return;

      var snapshot = wpfTextView.TextSnapshot;

      if (pos >= snapshot.Length)
        return;

      textViewModel.NavigateTo(wpfTextView.TextSnapshot, pos);
    }

    internal static void NavigateTo(IServiceProvider serviceProvider, string filename, NSpan span)
    {
      IVsTextView viewAdapter;
      IVsWindowFrame pWindowFrame;
      OpenDocument(serviceProvider, filename, out viewAdapter, out pWindowFrame);
      if (pWindowFrame == null || viewAdapter == null)
        return;
      ErrorHandler.ThrowOnFailure(pWindowFrame.Show());

      var wpfTextView = viewAdapter.ToIWpfTextView();
      var textViewModel = wpfTextView.TryGetTextViewModel();
      if (textViewModel == null)
        return;

      var snapshot = wpfTextView.TextSnapshot;

      if (span.EndPos >= snapshot.Length)
        return;

      textViewModel.NavigateTo(wpfTextView.TextSnapshot, span.StartPos);
      wpfTextView.Selection.Select(new SnapshotSpan(snapshot, new Span(span.StartPos, span.Length)), false);
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

    internal static void NavigateTo(IServiceProvider serviceProvider, string filename, int line, int col)
    {
      line--;
      col--;
      IVsTextView viewAdapter;
      IVsWindowFrame pWindowFrame;
      OpenDocument(serviceProvider, filename, out viewAdapter, out pWindowFrame);
      if (pWindowFrame == null)
        return;
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

    public static Rect? CalcActiveAreaRect(IWpfTextView wpfTextView, SnapshotSpan span)
    {
      var geometry = wpfTextView.TextViewLines.GetTextMarkerGeometry(span);

      if (geometry == null)
        return null;

      var visual = (Visual)wpfTextView;
      var bound = geometry.Bounds;

      bound.Offset(-wpfTextView.ViewportLeft, -wpfTextView.ViewportTop);
      bound.Height += 10;

      var rect = new Rect(visual.PointToScreen(bound.Location), bound.Size);
      return rect;
    }

    public static Rect? GetViewSpanRect(IWpfTextView wpfTextView, SnapshotSpan span)
    {
      var nullable = new Rect?();
      if (span.Length > 0)
      {
        double num1 = double.MaxValue;
        double num2 = double.MaxValue;
        double val1_1 = double.MinValue;
        double val1_2 = double.MinValue;
        foreach (TextBounds textBounds in wpfTextView.TextViewLines.GetNormalizedTextBounds(span))
        {
          num1 = Math.Min(num1, textBounds.Left);
          num2 = Math.Min(num2, textBounds.TextTop);
          val1_1 = Math.Max(val1_1, textBounds.Right);
          val1_2 = Math.Max(val1_2, textBounds.TextBottom + 1.0);
        }
        IWpfTextViewLine containingBufferPosition = wpfTextView.TextViewLines.GetTextViewLineContainingBufferPosition(span.Start);
        if (containingBufferPosition != null)
        {
          TextBounds extendedCharacterBounds = containingBufferPosition.GetExtendedCharacterBounds(span.Start);
          if (extendedCharacterBounds.Left < val1_1 && extendedCharacterBounds.Left >= wpfTextView.ViewportLeft && extendedCharacterBounds.Left < wpfTextView.ViewportRight)
            num1 = extendedCharacterBounds.Left;
        }
        ITextViewLine textViewLine = wpfTextView.TextViewLines.GetTextViewLineContainingBufferPosition(span.End);
        if (textViewLine != null && textViewLine.Start == span.End)
          val1_2 = Math.Max(val1_2, textViewLine.TextBottom + 1.0);
        if (num1 < val1_1)
          nullable = new Rect(num1, num2, val1_1 - num1, val1_2 - num2);
      }
      else
      {
        ITextViewLine textViewLine = wpfTextView.TextViewLines.GetTextViewLineContainingBufferPosition(span.Start);
        if (textViewLine != null)
        {
          TextBounds characterBounds = textViewLine.GetCharacterBounds(span.Start);
          nullable = new Rect(characterBounds.Left, characterBounds.TextTop, 0.0, characterBounds.TextHeight + 1.0);
        }
      }
      if (!nullable.HasValue || nullable.Value.IsEmpty)
        return null;
      Rect rect1 = new Rect(wpfTextView.ViewportLeft, wpfTextView.ViewportTop, wpfTextView.ViewportWidth, wpfTextView.ViewportHeight);
      Rect rect2 = nullable.Value;
      rect2.Intersect(rect1);
      var point1 = GetScreenPointFromTextXY(wpfTextView, rect2.Left, rect2.Top);
      var point2 = GetScreenPointFromTextXY(wpfTextView, rect2.Right, rect2.Bottom);
      return new Rect(point1, point2);
    }

    public static Point GetScreenPointFromTextXY(IWpfTextView wpfTextView, double x, double y)
    {
      return wpfTextView.VisualElement.PointToScreen(new Point(x - wpfTextView.ViewportLeft, y - wpfTextView.ViewportTop));
    }

    public static D.Color ToDColor(this int colorValue)
    {
      var bytes = BitConverter.GetBytes(colorValue);
      return D.Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
    }
    public static D.Color ToDColor(this SpanClassInfo spanClass) => ToDColor(spanClass.ForegroundColor);
  }
}
