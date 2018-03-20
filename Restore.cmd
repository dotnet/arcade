@echo off
powershell -ExecutionPolicy ByPass %~dp0eng\common\Build.ps1 -restore %*
exit /b %ErrorLevel%