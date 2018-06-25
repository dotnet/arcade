@echo off
powershell -ExecutionPolicy -NoProfile ByPass %~dp0eng\common\Build.ps1 -restore -build %*
exit /b %ErrorLevel%
