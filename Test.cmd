@echo off
powershell -ExecutionPolicy ByPass -NoProfile -File "%~dp0eng\common\build.ps1" -test %*