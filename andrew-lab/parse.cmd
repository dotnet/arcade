
@echo off
setlocal EnableDelayedExpansion


set "configuration_specified="

:argparser
:argparser_start
  if "%~1" == "" goto argparser_end
  set "argparser_currentarg=%~1"
  shift
  set "argparser_currentarg_prefix=%argparser_currentarg:~0,2%"
  IF "%argparser_currentarg_prefix%" == "--" (
    set "argparser_currentarg=%argparser_currentarg:~1%"
  )

  IF /i "%argparser_currentarg%"=="-c" (
    if defined configuration_specified (
        call :usage
        exit /b 0
    )
    if "%~1" == "" (
        call :usage
        exit /b 0
    )
    set "configuration_specified=1"
    set "configuration=%~1"
    goto :argparser_break
  )
  IF /i "%argparser_currentarg%"=="-configuration" (
    if defined configuration_specified (
        call :usage
        exit /b 0
    )
    if "%~1" == "" (
        call :usage
        exit /b 0
    )
    set "configuration_specified=1"
    set "configuration=%~1"
    goto :argparser_break
  )

  call :usage
  exit /b 0
  :argparser_break
  shift
goto argparser_start
:argparser_end

if not defined configuration_specified (
    call :usage
    exit /b 0
)

if defined configuration_specified (
    echo The configuration is '%configuration%'
)

exit /b 0

:usage
echo Usage
exit /b 0
