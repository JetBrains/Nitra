@echo off
call %~dp0\sub-env.cmd

echo ---- POSITIVE TESTS ----
set TeamCityArgs=
if defined TEAMCITY_VERSION set TeamCityArgs=-team-city-test-suite:Nitra_Positive
set OutDir=%~dp0\Bin\%Configuration%\Positive
set Tests=%~dp0\!Positive

call %~dp0\sub-run.cmd

if not defined RunNopause pause