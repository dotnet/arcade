@echo off
powershell -ExecutionPolicy ByPass -NoProfile %~dp0eng\common\Build.ps1 -restore -build %*
exit /b %ErrorLevel%
