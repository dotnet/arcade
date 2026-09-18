@echo off
powershell -ExecutionPolicy ByPass -NoProfile -File "%~dp0build.ps1" %*
exit /b %ErrorLevel%
