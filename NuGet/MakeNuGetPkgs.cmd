set version=%1
set Configuration=Debug
set Solution=..
set ExternalTools=%Solution%\ExternalTools
set NuGet=%ExternalTools%\NuGet.exe
set Output=%Solution%\bin\%Configuration%\NuGetSource
set Properties=Configuration=%Configuration%;Solution=%Solution%;version=%version%

echo Star %0
echo version='%version%'
dir
mkdir %Output%
%NuGet% pack %Solution%\Nitra\Nitra.Core\Nitra.Core.nproj -OutputDirectory %Output% -Properties "%Properties%" -IncludeReferencedProjects
%NuGet% pack %Solution%\NuGet\Nitra.Compiler.nuspec       -OutputDirectory %Output% -Properties "%Properties%"
%NuGet% pack %Solution%\NuGet\Nitra.VisualStudio.nuspec   -OutputDirectory %Output% -Properties "%Properties%"
%NuGet% pack %Solution%\NuGet\Nitra.Tools.nuspec          -OutputDirectory %Output% -Properties "%Properties%" -NoPackageAnalysis