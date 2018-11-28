@echo off
setlocal EnableDelayedExpansion EnableExtensions

set __BuildArgs=
set __UpdateSdkArgs=
set __UpdateSdk=0
set __GitHubPat=
set __BarToken=
set __ThisScriptDir=%~dp0

:Arg_Loop
set currentParam=%1

if "%currentParam%" == "" goto ArgsDone

if /i "%currentParam%" == "-updatesdk" (
    set __UpdateSdk=1
    goto ShiftAndLoop
)
if /i "%currentParam%" == "-gitHubPat" (
    set __UpdateSdkArgs=!__UpdateSdkArgs! %currentParam%
    set __GitHubPat=%2
    set __UpdateSdkArgs=!__UpdateSdkArgs! %2
    shift
    goto ShiftAndLoop
)
if /i "%currentParam%" == "-barToken" (
    set __UpdateSdkArgs=!__UpdateSdkArgs! %currentParam%
    set __BarToken=%2
    set __UpdateSdkArgs=!__UpdateSdkArgs! %2
    shift
    goto ShiftAndLoop
) 

if "%currentParam:~2,1%" == ":" (
    set value=%2
    set __BuildArgs=!__BuildArgs! %currentParam%=%2
    shift
    goto ShiftAndLoop
) else (
    set __BuildArgs=!__BuildArgs! %currentParam%
    goto ShiftAndLoop
)

:ShiftAndLoop
shift
goto Arg_Loop
    
:ErrorAndExit
echo There was an error while self building Arcade...
exit /b 1

:ArgsDone
if %__UpdateSdk% == 1 (
  if "%__GitHubPat%" == "" (
    goto ErrorAndExit
  )
  if "%__BarToken%" == "" (
    goto ErrorAndExit
  )
  
  echo Executing updatesdk.ps1...
  powershell -ExecutionPolicy ByPass -NoProfile -command "& """%__ThisScriptDir%eng\updatesdk.ps1""" %__UpdateSdkArgs%"
)

if %ErrorLevel% == 0 (
  echo Executing Build.ps1...
  powershell -ExecutionPolicy ByPass -NoProfile -command "& """%__ThisScriptDir%eng\common\Build.ps1""" -restore -build %__BuildArgs:"=%"
)

exit /b %ErrorLevel%