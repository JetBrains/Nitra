echo Executing %0
set version=%1
set Configuration=%2
set Solution=..
set ExternalTools=%Solution%\ExternalTools
set NuGet=%ExternalTools%\NuGet.exe
set Output=%Solution%\bin\%Configuration%\NuGetSource
set Properties=Configuration=%Configuration%;Solution=%Solution%;version=%version%

mkdir %Output%
%NuGet% pack %Solution%\Nitra\Nitra.Runtime\Nitra.Runtime.nproj -OutputDirectory %Output% -Properties "%Properties%"
%NuGet% pack %Solution%\NuGet\Nitra.Compiler.nuspec             -OutputDirectory %Output% -Properties "%Properties%"
%NuGet% pack %Solution%\NuGet\Nitra.VisualStudio.nuspec         -OutputDirectory %Output% -Properties "%Properties%"
%NuGet% pack %Solution%\NuGet\Nitra.Tools.nuspec                -OutputDirectory %Output% -Properties "%Properties%" -NoPackageAnalysis
echo End off %0
