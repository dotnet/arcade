@echo off

set __ValidateSdk=0
set __ThisScriptDir=%~dp0

:Arg_Loop
set currentParam=%1

 if "%currentParam%" == "" goto ArgsDone
 
 if /i "%currentParam%" == "-validateSdk" (
    set __ValidateSdk=1
)

shift
goto Arg_Loop

:ArgsDone

if %__ValidateSdk% == 1 (
    powershell -ExecutionPolicy ByPass -NoProfile -command "& """%__ThisScriptDir%..\ValidateSdk.ps1""" -restore -build -test -sign -pack -publish -ci %*"
) else (
    powershell -ExecutionPolicy ByPass -NoProfile -command "& """%__ThisScriptDir%Build.ps1""" -restore -build -test -sign -pack -publish -ci %*"
)
    
exit /b %ErrorLevel%
