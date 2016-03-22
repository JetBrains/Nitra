@echo off
title RebuildBoot
%WinDir%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe %~dp0\BootTasks.proj /t:BuildBoot /tv:4.0 /p:BuildTarget=Rebuild
pause