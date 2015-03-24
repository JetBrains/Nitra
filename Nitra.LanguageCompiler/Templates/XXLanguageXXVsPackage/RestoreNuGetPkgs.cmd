set ProjectDir=%~dp0
set NuGetLocalSource=%1
set SolutionDir=%ProjectDir%..\..\
set ExternalTools=%SolutionDir%\ExternalTools
set NuGet=%ExternalTools%\NuGet.exe

%NuGet% restore %ProjectDir%\packages.config -PackagesDirectory %ProjectDir%\packages -Source "%NuGetLocalSource%;https://www.nuget.org/api/v2"