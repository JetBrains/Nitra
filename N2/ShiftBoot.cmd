@echo off
title ShiftBoot
%WinDir%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe %~dp0\BootTasks.proj /t:ShiftBoot /tv:4.0
pause