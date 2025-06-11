@echo off

powershell -ExecutionPolicy ByPass -NoProfile -Command "& { . '%~dp0tools.ps1'; InitializeDotNetCli $true $true }"

if NOT [%ERRORLEVEL%] == [0] (
  echo Failed to install or invoke dotnet... 1>&2
  exit /b %ERRORLEVEL%
)

:: Invoke acquired SDK with args if they are provided
if NOT "%~1" == "" (
  set /p dotnetPath=<%~dp0..\..\artifacts\toolset\sdk.txt
  set DOTNET_NOLOGO=1
  call "%dotnetPath%\dotnet.exe" %*
)