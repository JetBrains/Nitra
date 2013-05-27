@echo off
set RunNopause=x
call %~dp0\run-positive.cmd nopause
call %~dp0\run-negative.cmd nopause
pause