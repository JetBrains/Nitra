echo off
set Nemerle="%ProgramFiles%\Nemerle\Net-4.0"
set Boot=boot\Net-4.0
set CompilerFiles=ncc.exe ncc.pdb ncc32.exe ncc32.pdb ncc64.exe ncc64.pdb Nemerle.dll Nemerle.pdb Nemerle.Compiler.dll Nemerle.Compiler.pdb Nemerle.Macros.dll Nemerle.Macros.pdb Nemerle.MSBuild.targets Nemerle.MSBuild.Tasks.dll

ROBOCOPY  N2.Compiler\bin\Debug\Stage2\   %Boot%\           N2.Compiler.???   /LOG:%Boot%\Boot-update.log /NP /NJS /V /NJH
ROBOCOPY  N2.Runtime\bin\Debug\           %Boot%\           N2.Runtime.???    /LOG+:%Boot%\Boot-update.log /NP /NJS /V /NJH
ROBOCOPY  N2.Grammar\bin\Debug\Stage2\    %Boot%\           N2.Grammar.???    /LOG+:%Boot%\Boot-update.log /NP /NJS /V /NJH
ROBOCOPY  %Nemerle%\                      %Boot%\           %CompilerFiles%   /LOG+:%Boot%\Boot-update.log /NP /NJS /V /NJH
ROBOCOPY  N2.Runtime\              Boot.N2.Runtime\         *.n *.nproj *.cs  /LOG+:%Boot%\Boot-update.log /NP /NJS /V /NJH /MIR /A+:R /XF Boot.N2.Runtime.nproj /XD obj bin
cls
echo Result of update %Boot%
type %Boot%\Boot-update.log
pause