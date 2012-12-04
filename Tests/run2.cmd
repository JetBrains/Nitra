rem @echo off
set Configuration=Debug
set Nemerle="%ProgramFiles%\Nemerle\Net-4.0"
set OutDir=Bin\%Configuration%\Positive
set RuntimeDllPath=..\N2\N2.Runtime\bin\%Configuration%

rmdir %OutDir% /S /Q
mkdir %OutDir%
rem copy %Nemerle%\Nemerle.* %OutDir%\Nemerle.* /B /Z
rem copy %RuntimeDllPath%\N2.Runtime.* %OutDir%  /B /Z
pause
copy Bin\Grammars\*.* %OutDir% /B /Z
pause

%Nemerle%\Nemerle.Compiler.Test.exe Positive\*.n Positive\*.cs Positive\*.n2 -output:%OutDir% -ref:System.Core -ref:%RuntimeDllPath%\N2.Runtime.dll
pause
