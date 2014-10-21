using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.Shell.Interop
{
  /// <summary>
  /// Implemented by clients interested in solution events. Subscribe to these events via <see cref="M:Microsoft.VisualStudio.Shell.Interop.IVsSolution.AdviseSolutionEvents(Microsoft.VisualStudio.Shell.Interop.IVsSolutionEvents,System.UInt32@)"/>.
  /// </summary>
  [Guid("6ACFF38A-0D6C-4792-B9D2-9469D60A2AD7")]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [ComImport]
  public interface IVsSolutionLoadEvents
  {
    /// <summary>
    /// Fired before a solution open begins. Extenders can activate a solution load manager by setting <see cref="F:Microsoft.VisualStudio.Shell.Interop.__VSPROPID4.VSPROPID_ActiveSolutionLoadManager"/>.
    /// </summary>
    ///
    /// <returns>
    /// If the method succeeds, it returns <see cref="F:Microsoft.VisualStudio.VSConstants.S_OK"/>. If it fails, it returns an error code.
    /// </returns>
    /// <param name="pszSolutionFilename">The name of the solution file.</param>
    [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    int OnBeforeOpenSolution([ComAliasName("OLE.LPCOLESTR"), MarshalAs(UnmanagedType.LPWStr), In] string pszSolutionFilename);

    /// <summary>
    /// Fired when background loading of projects is beginning again after the initial solution open operation has completed.
    /// </summary>
    ///
    /// <returns>
    /// If the method succeeds, it returns <see cref="F:Microsoft.VisualStudio.VSConstants.S_OK"/>. If it fails, it returns an error code.
    /// </returns>
    [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    int OnBeforeBackgroundSolutionLoadBegins();

    /// <summary>
    /// Fired before background loading a batch of projects. Normally a background batch loads a single pending project. This is a cancelable event.
    /// </summary>
    ///
    /// <returns>
    /// If the method succeeds, it returns <see cref="F:Microsoft.VisualStudio.VSConstants.S_OK"/>. If it fails, it returns an error code.
    /// </returns>
    /// <param name="pfShouldDelayLoadToNextIdle">[out] true if other background operations should complete before starting to load the project, otherwise false.</param>
    [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    int OnQueryBackgroundLoadProjectBatch(out bool pfShouldDelayLoadToNextIdle);

    /// <summary>
    /// Fired when loading a batch of dependent projects as part of loading a solution in the background.
    /// </summary>
    ///
    /// <returns>
    /// If the method succeeds, it returns <see cref="F:Microsoft.VisualStudio.VSConstants.S_OK"/>. If it fails, it returns an error code.
    /// </returns>
    /// <param name="fIsBackgroundIdleBatch">true if the batch is loaded in the background, otherwise false.</param>
    [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    int OnBeforeLoadProjectBatch([In] bool fIsBackgroundIdleBatch);

    /// <summary>
    /// Fired when the loading of a batch of dependent projects is complete.
    /// </summary>
    ///
    /// <returns>
    /// If the method succeeds, it returns <see cref="F:Microsoft.VisualStudio.VSConstants.S_OK"/>. If it fails, it returns an error code.
    /// </returns>
    /// <param name="fIsBackgroundIdleBatch">true if the batch is loaded in the background, otherwise false.</param>
    [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    int OnAfterLoadProjectBatch([In] bool fIsBackgroundIdleBatch);

    /// <summary>
    /// Fired when the solution load process is fully complete, including all background loading of projects.
    /// </summary>
    ///
    /// <returns>
    /// If the method succeeds, it returns <see cref="F:Microsoft.VisualStudio.VSConstants.S_OK"/>. If it fails, it returns an error code.
    /// </returns>
    [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
    int OnAfterBackgroundSolutionLoadComplete();
  }
}
