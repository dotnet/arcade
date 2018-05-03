@echo off
powershell -NoProfile -NoLogo -ExecutionPolicy ByPass %~dp0nativetoolsbootstrap.ps1 %*
exit /b %ErrorLevel%