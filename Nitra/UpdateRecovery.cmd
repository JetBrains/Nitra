@echo off
title UpdateRecovery
xcopy %~dp0\..\Nitra.Visualizer\Recovery\Recovery.cs %~dp0\Nitra.Runtime\Internal\Recovery /R /Y
pause