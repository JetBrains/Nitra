@echo off
%WinDir%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe %~dp0\BootTasks.proj /t:ShiftRuntime /tv:4.0
if errorlevel 1 goto end
%WinDir%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe %~dp0\BootTasks.proj /t:BuildBoot /tv:4.0
:end
pause