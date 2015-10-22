using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
//using Microsoft.VisualStudio.Project;
using Microsoft.VisualStudio.Shell;
using Nemerle.Compiler;
//using Nemerle.VisualStudio.LanguageService;
//using Msbuild = Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Text;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using System.Diagnostics;
using Microsoft.VisualStudio;
//using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Nitra.VisualStudio
{
  public static partial class NitraVsUtils
  {
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
        throw new InvalidOperationException("The IVsTextView shims should allow us to cast to IVsUserData");
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
  }
}
