set WORKSPACEPATH=%1
REM Deleting %WORKSPACEPATH% in case it happens to be a file
del /Q %WORKSPACEPATH%
rmdir /S /Q %WORKSPACEPATH%
mkdir %WORKSPACEPATH%
xcopy /Y /S /I %HELIX_CORRELATION_PAYLOAD%\* %WORKSPACEPATH%
copy /Y %HELIX_WORKITEM_PAYLOAD%\.agent %WORKSPACEPATH%
copy /Y %HELIX_WORKITEM_PAYLOAD%\.credentials %WORKSPACEPATH%
%WORKSPACEPATH%\run.cmd

set LASTEXITCODE=%errorlevel%
REM Expect an exit code of 2, which is what is given when the agent connection is revoked
if not "%LASTEXITCODE%" == "2" (
	echo "Unexpected error returned from agent: %LASTEXITCODE%"
	exit /b 1
) else (
	echo "Agent disconnected successfully, exiting"
	exit /b 0
)
