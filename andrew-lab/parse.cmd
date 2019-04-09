
@echo off
setlocal EnableDelayedExpansion
set /a "configuration_specification_count=0"
:argparser
:argparser_start
  if "%~1" == "" goto argparser_end
  set "argparser_currentarg=%~1"
  shift
  set "argparser_currentarg_prefix=%argparser_currentarg:~0,2%"
  if "%argparser_currentarg_prefix%" == "--" (
    set "argparser_currentarg=%argparser_currentarg:~1%"
  )
  if /i "%argparser_currentarg%"=="-c" ( set /a "configuration_specification_count=!configuration_specification_count!+1" )
  if /i "%argparser_currentarg%"=="-configuration" ( set /a "configuration_specification_count=!configuration_specification_count!+1" )
  if "%configuration_specification_count%" GEQ "2" ( goto usage )
  if "%configuration_specification_count%" == "1" (
    if "%~1" == "" ( goto usage )
    set "configuration=%~1"
    goto argparser_break
  )
  goto usage
  :argparser_break
  shift
goto argparser_start
:argparser_end
if "%configuration_specification_count%" == "0"  ( goto usage )
if "%configuration_specification_count%" == "1"  (
    echo The configuration is '%configuration%'
)
exit /b 0
:usage
echo Usage
exit /b 1