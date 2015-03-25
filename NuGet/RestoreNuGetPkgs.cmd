set SolutionDir=%~dp0..\
set ProjectDir=%1
set NuGetLocalSource=%2
set ExternalTools=%SolutionDir%\ExternalTools
set NuGet=%ExternalTools%\NuGet.exe

%NuGet% update  %ProjectDir%\packages.config -Source "%NuGetLocalSource%"
%NuGet% restore %ProjectDir%\packages.config -PackagesDirectory %ProjectDir%\packages -Source "%NuGetLocalSource%;https://www.nuget.org/api/v2"