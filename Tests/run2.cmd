@echo off
if not defined Configuration set Configuration=Debug
if not defined NemerleBinPathRoot set NemerleBinPathRoot="%ProgramFiles%\Nemerle"
if not defined Nemerle set Nemerle=%NemerleBinPathRoot%\Net-4.0
set RuntimeDllPath=%~dp0\..\N2\N2.Runtime\bin\%Configuration%
set N2CompilerDllPath=%~dp0\..\N2\N2.Compiler\bin\%Configuration%\Stage1

echo ---- POSITIVE TESTS ----
set TeamCityArgs=
if defined TEAMCITY_VERSION set TeamCityArgs=-team-city-test-suite:Nitra_Positive
set OutDir=%~dp0\Bin\%Configuration%\Positive
set Tests=%~dp0\!Positive
call :runtests

echo ---- NEGATIVE TESTS ----
set TeamCityArgs=
if defined TEAMCITY_VERSION set TeamCityArgs=-team-city-test-suite:Nitra_Negative
set OutDir=%~dp0\Bin\%Configuration%\Negative
set Tests=%~dp0\!Negative
call :runtests

pause

goto :eof


:runtests
rmdir %OutDir% /S /Q
mkdir %OutDir%
copy %~dp0\..\Grammars\Bin\%Configuration%\*.* %OutDir% /B /Z 1>nul
pushd .
cd %OutDir%
%Nemerle%\Nemerle.Compiler.Test.exe %Tests%\*.n %Tests%\*.cs %Tests%\*.n2 -output:%OutDir% -ref:System.Core -ref:%RuntimeDllPath%\N2.Runtime.dll -macro:%N2CompilerDllPath%\N2.Compiler.dll %TeamCityArgs%
popd
goto :eof