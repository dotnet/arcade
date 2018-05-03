@echo off
powershell -NoProfile -NoLogo -ExecutionPolicy ByPass %~dp0init-native-tools.ps1 %*
exit /b %ErrorLevel%