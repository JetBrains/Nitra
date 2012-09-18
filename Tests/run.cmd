@echo off
%~dp0\Bin\Framework\N2.Test.exe -diff -create-gold "%~dp0\Sources\*.test"
pause
