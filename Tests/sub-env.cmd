set NGen="%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\ngen.exe"

if not defined Configuration set Configuration=Debug
if not defined NemerleBinPathRoot set NemerleBinPathRoot=%ProgramFiles%\Nemerle
if not defined Nemerle set Nemerle=%NemerleBinPathRoot%\Net-4.0
set RuntimeDllPath=%~dp0\..\N2\Nitra.Runtime\bin\%Configuration%
set CoreDllPath=%~dp0\..\N2\Nitra.Core\bin\%Configuration%
set N2CompilerDllPath=%~dp0\..\N2\Nitra.Compiler\bin\%Configuration%\Stage1
rem for %%d in (%N2CompilerDllPath%\*.dll) DO %NGen% install %%d
