@echo off
setlocal EnableDelayedExpansion
set "variable_a_specified="
set "variable_b_specified="
:argparser
:argparser_start
  if "%~1" == "" goto argparser_end
  set "argparser_currentarg=%~1"
  shift
  set "argparser_currentarg_prefix=%argparser_currentarg:~0,2%"
  if "%argparser_currentarg_prefix%" == "--" (
    set "argparser_currentarg=%argparser_currentarg:~1%"
  )
  set "variable_a_specified_inloop="
  if /i "%argparser_currentarg%"=="-a" ( set "variable_a_specified_inloop=1" )
  if /i "%argparser_currentarg%"=="-parameter-a" ( set "variable_a_specified_inloop=1" )
  if defined variable_a_specified_inloop (
    if defined variable_a_specified ( goto usage )
    if "%~1" == "" ( goto usage )
    set "variable_a_specified=1"
    set "variable_a=%~1"
    goto argparser_break
  )
  set "variable_b_specified_inloop="
  if /i "%argparser_currentarg%"=="-b" ( set "variable_b_specified_inloop=1" )
  if /i "%argparser_currentarg%"=="-parameter-b" ( set "variable_b_specified_inloop=1" )
  if defined variable_b_specified_inloop (
    if defined variable_b_specified ( goto usage )
    if "%~1" == "" ( goto usage )
    set "variable_b_specified=1"
    set "variable_b=%~1"
    goto argparser_break
  )
  goto usage
  :argparser_break
  shift
goto argparser_start
:argparser_end
if not defined variable_a_specified ( goto usage )
if defined variable_a_specified (
    echo The variable_a is '%variable_a%'
)
if defined variable_b_specified (
    echo The variable_b is '%variable_b%'
)
exit /b 0
:usage
echo Usage
exit /b 1