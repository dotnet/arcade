@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0validate-sdk.ps1""" %*"
