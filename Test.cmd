@echo off
powershell -ExecutionPolicy ByPass -NoProfile -File "%~dp0eng\common\Build.ps1" -test %*