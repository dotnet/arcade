@echo off
powershell -ExecutionPolicy ByPass -NoProfile %~dp0Build.ps1 -restore -build -test -sign -pack -ci %*
exit /b %ErrorLevel%
