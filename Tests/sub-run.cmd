@echo off
if exist %OutDir% rmdir %OutDir% /S /Q
mkdir %OutDir%
copy %~dp0\..\Grammars\Bin\%Configuration%\*.* %OutDir% /B /Z 1>nul
pushd .
cd %OutDir%
"%Nemerle%\Nemerle.Compiler.Test.exe" %Tests%\*.n %Tests%\*.cs %Tests%\*.nitra -output:%OutDir% -ref:System.Core -ref:%RuntimeDllPath%\Nitra.Runtime.dll -ref:%CoreDllPath%\Nitra.Core.dll -macro:%N2CompilerDllPath%\Nitra.Compiler.dll %TeamCityArgs%
popd