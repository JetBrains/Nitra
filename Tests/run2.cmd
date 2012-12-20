@echo off
set Configuration=Debug
rem set Configuration=Release
set Nemerle="%ProgramFiles%\Nemerle\Net-4.0"
set OutDir=%~dp0\Bin\%Configuration%\Positive
set RuntimeDllPath=%~dp0\..\N2\N2.Runtime\bin\%Configuration%
set Positive=%~dp0\!Positive
rmdir %OutDir% /S /Q
mkdir %OutDir%
copy %~dp0\..\Grammars\Bin\%Configuration%\*.* %OutDir% /B /Z 1>nul
pushd .
cd %OutDir%
%Nemerle%\Nemerle.Compiler.Test.exe %Positive%\*.n %Positive%\*.cs %Positive%\*.n2 -output:%OutDir% -ref:System.Core -ref:%RuntimeDllPath%\N2.Runtime.dll
popd
pause
