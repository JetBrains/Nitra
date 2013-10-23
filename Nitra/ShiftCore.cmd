@echo off
title ShiftCore
%WinDir%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe %~dp0\BootTasks.proj /t:ShiftCore /tv:4.0
pause