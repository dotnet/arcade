@echo off
powershell -NoProfile -NoLogo -ExecutionPolicy ByPass -File "%~dp0init-tools-native.ps1" %*
exit /b %ErrorLevel%