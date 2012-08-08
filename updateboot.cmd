echo off
set Nemerle="%ProgramFiles%\Nemerle\Net-4.0"
set Boot=boot\Net-4.0
set CompilerFiles=ncc.exe ncc.pdb ncc32.exe ncc32.pdb ncc64.exe ncc64.pdb Nemerle.dll Nemerle.pdb Nemerle.Compiler.dll Nemerle.Compiler.pdb Nemerle.Macros.dll Nemerle.Macros.pdb Nemerle.MSBuild.targets Nemerle.MSBuild.Tasks.dll

ROBOCOPY Nemerle.Parser.Macro\bin\Debug\        %Boot%\   Nemerle.Parser.Macro.???           /LOG:%Boot%\Boot-update.log /NP /NJS /V /NJH
ROBOCOPY Nemerle.Parser\bin\Debug\              %Boot%\   Nemerle.Parser.???                /LOG+:%Boot%\Boot-update.log /NP /NJS /V /NJH
ROBOCOPY Nemerle.Parser.Macro.Parser\bin\Debug\ %Boot%\   Nemerle.Parser.Macro.Parser.???   /LOG+:%Boot%\Boot-update.log /NP /NJS /V /NJH
ROBOCOPY Nemerle.Parser.Macro.Model\bin\Debug\  %Boot%\   Nemerle.Parser.Macro.Model.???    /LOG+:%Boot%\Boot-update.log /NP /NJS /V /NJH
ROBOCOPY %Nemerle%\                             %Boot%\   %CompilerFiles%                   /LOG+:%Boot%\Boot-update.log /NP /NJS /V /NJH
ROBOCOPY Nemerle.Parser\                        Boot.Nemerle.Parser\   *.n *.nproj          /LOG+:%Boot%\Boot-update.log /NP /NJS /V /NJH /MIR /A+:R /XF Boot.Nemerle.Parser.nproj 
cls
echo Result of update %Boot%
type %Boot%\Boot-update.log
pause