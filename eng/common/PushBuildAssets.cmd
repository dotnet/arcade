@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0Build.ps1""" -pushBuildAssets %*"
exit /b %ErrorLevel%
