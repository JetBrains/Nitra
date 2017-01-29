@echo off
title UpdateStage1Metadata
xcopy %~dp0\Nitra\Nitra.Runtime\obj\Debug\Stage2\Nitra.Runtime.Stage2.dll.nitrametadata2 %~dp0\Nitra\Nitra.Runtime /R /Y
xcopy %~dp0\Nitra\DotNetLang\obj\Debug\Stage2\DotNetLang.Stage2.dll.nitrametadata2 %~dp0\Nitra\DotNetLang /R /Y
pause