@setlocal enableextensions enabledelayedexpansion
@echo off

set __ThisScriptDir=%~dp0
set arglist=%*

REM use substitution to determine if '-validateSdk' is in the argument list
if not x"%arglist:-validateSdk=%" == x"%arglist%" (
    powershell -ExecutionPolicy ByPass -NoProfile -command "& """%__ThisScriptDir%..\ValidateSdk.ps1""" -restore -build -test -sign -pack -publish -ci %*"
) else (
    powershell -ExecutionPolicy ByPass -NoProfile -command "& """%__ThisScriptDir%Build.ps1""" -restore -build -test -sign -pack -publish -ci %*"
)
endlocal