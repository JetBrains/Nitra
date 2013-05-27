@echo off
call %~dp0\sub-env.cmd

echo ---- NEGATIVE TESTS ----
set TeamCityArgs=
if defined TEAMCITY_VERSION set TeamCityArgs=-team-city-test-suite:Nitra_Negative
set OutDir=%~dp0\Bin\%Configuration%\Negative
set Tests=%~dp0\!Negative

call %~dp0\sub-run.cmd

if not defined RunNopause pause