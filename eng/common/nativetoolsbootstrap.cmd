@echo off
powershell -ExecutionPolicy ByPass %~dp0nativetoolsbootstrap.ps1 %*
exit /b %ErrorLevel%