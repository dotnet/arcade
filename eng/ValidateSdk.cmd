@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0validatesdk.ps1""" -packageSource "%~dp0..\artifacts\packages\debug\NonShipping" %*"
exit /b %ErrorLevel%