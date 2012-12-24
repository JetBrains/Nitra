@echo off
%WinDir%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe %~dp0\boottasks.proj /t:BuildBoot /tv:4.0
pause