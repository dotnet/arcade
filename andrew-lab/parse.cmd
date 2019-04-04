@echo off
setlocal EnableDelayedExpansion

REM Prevent affecting possible callers of the batch
REM Without delayed expansion, !arg%argno%! used below won't work.

set configuration_specified=0
:argparser
:argparser_start
  if -%1-==-- goto argparser_end
  set argparser_currentarg=%1
  shift
  set argparser_currentarg_prefix=%argparser_currentarg:~0,2%
  IF "%argparser_currentarg_prefix%" == "--" (
    set argparser_currentarg=%argparser_currentarg:~1%
  )
  IF /i "%argparser_currentarg%"=="-c" (
    if "%configuration_specified%"=="1" (
        call :usage
        exit /b
    )
    if -%1-==-- (
        call :usage
        exit /b
    )
    set configuration_specified=1
    set configuration=%1
    goto :argparser_break
  )
  IF /i "%argparser_currentarg%"=="-configuration" (
    if "%configuration_specified%"=="1" (
        call :usage
        exit /b
    )
    if -%1-==-- (
        call :usage
        exit /b
    )
    set configuration_specified=1
    set configuration=%1
    goto :argparser_break
  )
  call :usage
  exit /b
  :argparser_break
  shift
goto argparser_start
:argparser_end

if "%configuration_specified%"=="0" (
    call :usage
    exit /b
)

echo The configuration is '%configuration%'
exit /b

:usage
echo Usage
exit /b