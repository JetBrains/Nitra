using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.ExtensionManager;
using System.IO;
using Microsoft.Win32;
using System.Globalization;
using Microsoft.VisualStudio.Settings;

namespace Nitra.MSBuild.Tasks
{
    public class InstallVsix : Task
    {
        [Required]
        public string VsixPath { get; set; }

        [Required]
        public string RootSuffix { get; set; }

        public string VisualStudioVersion { get; set; }

        class VisualStudio
        { 
            public readonly decimal Version;
            public readonly string ExePath;

            public VisualStudio(decimal version, string exePath)
            {
                Version = version;
                ExePath = exePath;
            }
        }

        public override bool Execute()
        {
            try
            {
                if (!File.Exists(VsixPath))
                    throw new Exception("Cannot find VSIX file " + VsixPath);

                var visualStudios = GetAllVisualStudios();
                if (visualStudios.Length == 0)
                    throw new Exception("Cannot find any installed copies of Visual Studio.");

                var vsix = ExtensionManagerService.CreateInstallableExtension(VsixPath);

                foreach (var vs in visualStudios)
                {
                    Console.WriteLine("Installing {0} version {1} to Visual Studio {2} /RootSuffix {3}",
                        vsix.Header.Name, vsix.Header.Version, vs.Version, RootSuffix);

                    Install(vs.ExePath, vsix, RootSuffix);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
                return false;
            }
        }

        static VisualStudio[] GetAllVisualStudios()
        {
            using (var software = Registry.LocalMachine.OpenSubKey("SOFTWARE"))
            using (var ms = software.OpenSubKey("Microsoft"))
            using (var vs = ms.OpenSubKey("VisualStudio"))
                return vs.GetSubKeyNames()
                        .Select(s =>
                {
                    decimal v;
                    if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out v))
                        return new decimal?();
                    return v;
                })
                .Where(x => x.HasValue)
                .Select(x => x.Value)
                .OrderBy(x => x)
                .Select(version =>
                    {
                        var key = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\" + version + @"\Setup\VS";
                        var exePath = Registry.GetValue(key, "EnvironmentPath", null) as string;
                        return exePath != null ? new VisualStudio(version, exePath) : null;
                    })
                .Where(x => x != null)
                .ToArray();
        }

        static void Install(string vsExe, IInstallableExtension vsix, string rootSuffix)
        {
            using (var esm = ExternalSettingsManager.CreateForApplication(vsExe, rootSuffix))
            {
                var ems = new ExtensionManagerService(esm);
                var installed = ems.GetInstalledExtensions().FirstOrDefault(x => x.Header.Identifier == vsix.Header.Identifier);
                if (installed != null)
                    ems.Uninstall(installed);
                ems.Install(vsix, perMachine: false);
            }
        }
    }
}
