set NGen="%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\ngen.exe"

if not defined Configuration set Configuration=Debug
if not defined NemerleBinPathRoot set NemerleBinPathRoot=%ProgramFiles%\Nemerle
if not defined Nemerle set Nemerle=%NemerleBinPathRoot%\Net-4.0
set RuntimeDllPath=%~dp0\..\Nitra\Nitra.Runtime\bin\%Configuration%
set CoreDllPath=%~dp0\..\Nitra\Nitra.Core\bin\%Configuration%
set CompilerDllPath=%~dp0\..\Nitra\Nitra.Compiler\bin\%Configuration%\Stage1
set QuoteDllPath=%~dp0\..\Nitra\Nitra.Quote\bin\%Configuration%
rem for %%d in (%CompilerDllPath%\*.dll) DO %NGen% install %%d
