set WORKSPACEPATH=%1
REM Deleting %WORKSPACEPATH% in case it happens to be a file
del /Q %WORKSPACEPATH%
rmdir /S /Q %WORKSPACEPATH%
mkdir %WORKSPACEPATH%
xcopy /Y /S /I %HELIX_CORRELATION_PAYLOAD%\* %WORKSPACEPATH%
copy /Y %HELIX_WORKITEM_PAYLOAD%\.agent %WORKSPACEPATH%
copy /Y %HELIX_WORKITEM_PAYLOAD%\.credentials %WORKSPACEPATH%
call %WORKSPACEPATH%\run.cmd

set LASTEXITCODE=%errorlevel%
if not "%LASTEXITCODE%" == "0" (
	echo "Unexpected error returned from agent: %LASTEXITCODE%"
	exit /b 1
) else (
	echo "Agent disconnected successfully, exiting"
	exit /b 0
)
