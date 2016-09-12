// Guids.cs
// MUST match guids.h
using System;

namespace XXNamespaceXX
{
    internal static class XXLanguageXXGuids
    {
        /// <summary>The GUID for this package.</summary>
        public const string PackageGuid                           = "7751d8fd-73c8-40bd-be2b-7aa6f7a74faa";
        public const string GuidXXLanguageXXVsPackageCmdSetString = "1E508B08-3BEF-4197-81C1-06B84A49F118";
        public const string GuidProject                           = "09CD39E9-5139-48B5-A1AE-B8EB59CEE1CD";

        public static readonly Guid GuidXXLanguageXXVsPackageCmdSet = new Guid(GuidXXLanguageXXVsPackageCmdSetString);
    };
}