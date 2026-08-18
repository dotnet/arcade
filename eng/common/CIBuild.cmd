@echo off
powershell -ExecutionPolicy ByPass -NoProfile -File "%~dp0Build.ps1" -restore -build -test -sign -pack -publish -ci %*
