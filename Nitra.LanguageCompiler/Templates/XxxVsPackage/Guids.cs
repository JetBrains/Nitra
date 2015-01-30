// Guids.cs
// MUST match guids.h
using System;

namespace XCompanyX.XxxVsPackage
{
    static class GuidList
    {
        public const string GuidXxxVsPackagePkgString    = "85c0a77e-a20e-4f36-a1e4-1ec513b41b0e";
        public const string GuidXxxVsPackageCmdSetString = "1e508b08-3bef-4197-81c1-06b84a49f118";
        public const string GuidProject                  = "09CD39E9-5139-48B5-A1AE-B8EB59CEE1CD";

        public static readonly Guid GuidXxxVsPackageCmdSet = new Guid(GuidXxxVsPackageCmdSetString);
    };
}