@echo off
title UpdateRecovery
xcopy %~dp0\..\N2.Visualizer\Recovery.cs %~dp0\N2.Runtime\Internal\Recovery /R /Y
pause