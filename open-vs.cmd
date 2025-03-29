@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0open-vs.ps1""" -vs %*"