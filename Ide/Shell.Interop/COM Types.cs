using System;
using System.Runtime.InteropServices;

// These types don't have a publicly available PIA, so I need to make one myself

[assembly: PrimaryInteropAssembly(12, 0)]
namespace Microsoft.Internal.VisualStudio.Shell.Interop
{
    [Guid("753E55C6-E779-4A7A-BCD1-FD87181D52C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IVsExtensionManagerPrivate
    {
        [PreserveSig]
        int GetEnabledExtensionContentLocations([MarshalAs(UnmanagedType.LPWStr)] [In] string szContentTypeName, [In] uint cContentLocations, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.BStr, SizeParamIndex = 1)] [Out] string[] rgbstrContentLocations, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.BStr, SizeParamIndex = 1)] [Out] string[] rgbstrUniqueExtensionStrings, out uint pcContentLocations);
        [PreserveSig]
        int GetEnabledExtensionContentLocationsWithNames([MarshalAs(UnmanagedType.LPWStr)] [In] string szContentTypeName, [In] uint cContentLocations, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.BStr, SizeParamIndex = 1)] [Out] string[] rgbstrContentLocations, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.BStr, SizeParamIndex = 1)] [Out] string[] rgbstrUniqueExtensionStrings, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.BStr, SizeParamIndex = 1)] [Out] string[] rgbstrExtensionNames, out uint pcContentLocations);
        [PreserveSig]
        int GetDisabledExtensionContentLocations([MarshalAs(UnmanagedType.LPWStr)] [In] string szContentTypeName, [In] uint cContentLocations, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.BStr, SizeParamIndex = 1)] [Out] string[] rgbstrContentLocations, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.BStr, SizeParamIndex = 1)] [Out] string[] rgbstrUniqueExtensionStrings, out uint pcContentLocations);
        [PreserveSig]
        int GetLastConfigurationChange([MarshalAs(UnmanagedType.LPArray)] [Out] DateTime[] pTimestamp);
        [PreserveSig]
        int LogAllInstalledExtensions();
        [PreserveSig]
        int GetUniqueExtensionString([MarshalAs(UnmanagedType.LPWStr)] [In] string szExtensionIdentifier, [MarshalAs(UnmanagedType.BStr)] out string pbstrUniqueString);
    }
    [Guid("6B741746-E3C9-434A-9E20-6E330D88C7F6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IVsExtensionManagerPrivate2
    {
        void GetAssetProperties([MarshalAs(UnmanagedType.LPWStr)] [In] string szAssetTypeName, [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out Array prgsaNames, [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out Array prgsaVersions, [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out Array prgsaAuthors, [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out Array prgsaExtensionIDs);
        void GetExtensionProperties([MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out Array prgsaNames, [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out Array prgsaVersions, [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out Array prgsaAuthors, [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out Array prgsaContentLocations, [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out Array prgsaExtensionIDs);
        ulong GetLastWriteTime([MarshalAs(UnmanagedType.LPWStr)] [In] string szContentTypeName);
    }
}
