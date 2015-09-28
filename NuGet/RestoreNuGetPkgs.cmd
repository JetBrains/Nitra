set SolutionDir=%~dp0..\
set ProjectDir=%1
set NuGetSource=%2
set ExternalTools=%SolutionDir%\ExternalTools
set NuGet=%ExternalTools%\NuGet.exe

%NuGet% update  %ProjectDir%\packages.config -Source %NuGetSource%
%NuGet% restore %ProjectDir%\packages.config -PackagesDirectory %ProjectDir%\packages -Source %NuGetSource%