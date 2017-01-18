$nemerleVersion = "1.2.489.0"
$nemerleDir = "C:\Program Files (x86)\Nemerle\net-4.0"
$temporarySetupFolder = "C:\NemerleTmp"

Write-Host "Nemerle version: $nemerleVersion"


# Create the $temporarySetupFolder
New-Item -ItemType directory -Path $temporarySetupFolder

Write-Host "Downloading nemerle setup"
# Download the nemerle setup for the $nemerleVersion
(new-object net.webclient).DownloadFile("http://nemerle.org/Download/Nightly%20master-NET40-VS2010/build-270/NemerleSetup-net-4.0-v$nemerleVersion.msi", "$temporarySetupFolder\NemerleSetup.msi")

Write-Host "Installing nemerle setup"
# Install the nemerle setup
Start-Process C:\Windows\System32\msiexec.exe -ArgumentList "/i $temporarySetupFolder\NemerleSetup.msi /qn /L*V $temporarySetupFolder\install.log" -wait

# print setup log
# Get-Content -Path "$temporarySetupFolder\install.log"

# prepare packing nemerle
$Nemerle_nuspec = @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>Nemerle</id>
    <version>$nemerleVersion</version>
    <title>Nemerle Runtime</title>
    <authors>Nemerle Team</authors>
    <description>Nemerle runtime with utility libraries.</description>
    <projectUrl>http://nemerle.org/</projectUrl>
    <licenseUrl>http://opensource.org/licenses/BSD-3-Clause</licenseUrl>
  </metadata>
  <files>
    <file src="Nemerle.dll"      target="lib\net\" />
    <file src="Nemerle.Peg.dll"  target="lib\net\" />
    <file src="Nemerle.Diff.dll" target="lib\net\" />
  </files>
</package>
"@

#write nuspec
$Nemerle_nuspec >> "$nemerleDir\Nemerle.nuspec"
Get-Content -Path "$nemerleDir\Nemerle.nuspec"

#pack nemerle nuget package
$packArgs = 'pack "{0}\Nemerle.nuspec" -BasePath "{0}" -OutputDirectory "{0}"' -f $nemerleDir
$nugetPath = (get-item env:NuGet).Value

Write-Host $packArgs
Write-Host $nugetPath

Start-Process $nugetPath -ArgumentList "$packArgs" -wait -NoNewWindow

Write-Host "Finished creating nemerle nuget"

#Call the clear script here, if files are missing it will return a returncode != 0 which makes trouble in appveyor
C:\NitraBase\Nitra\Clear.cmd