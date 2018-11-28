@echo off
CALL %~dp0..\Build.cmd -test -sign -pack -publish -ci -updatesdk %*
exit /b %ErrorLevel%