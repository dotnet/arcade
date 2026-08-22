@echo off
powershell -ExecutionPolicy ByPass -NoProfile -File "%~dp0build.ps1" -restore -build -test -sign -pack -publish -ci %*
