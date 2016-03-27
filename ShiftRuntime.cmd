@echo off
title ShiftRuntime
%WinDir%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe %~dp0\Common\BootTasks.proj /t:ShiftRuntime /tv:4.0
pause