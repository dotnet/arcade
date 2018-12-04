@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0validatesdk.ps1""" %*"
exit /b %ErrorLevel%