@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0Validate-Sdk.ps1""" -restore -build -test -sign -pack -publish -ci %*"
exit /b %ErrorLevel%