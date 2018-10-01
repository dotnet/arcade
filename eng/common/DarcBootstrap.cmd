@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0darc-bootstrap.ps1""""
exit /b %ErrorLevel%