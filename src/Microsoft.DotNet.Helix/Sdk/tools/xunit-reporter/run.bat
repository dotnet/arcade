
set ENV_PATH=%USERPROFILE%\.xunit-reporter-env
set TMP_ENV_PATH=%USERPROFILE%\.xunit-reporter-env-tmp

REM Removing pythonpath forces a clean installation of pip packages, but subsequent commands may use HELIX libraries
set _OLD_PYTHONPATH=%PYTHONPATH%
set PYTHONPATH=

echo  %date%-%time%

if NOT EXIST %ENV_PATH%\Scripts\python.exe (
  rmdir /Q /S %TMP_ENV_PATH%
  rmdir /Q /S %ENV_PATH%
  %HELIX_PYTHONPATH% -m virtualenv --no-site-packages %TMP_ENV_PATH%
  rename %TMP_ENV_PATH% .xunit-reporter-env
)
REM On certain slow machines python.exe keeps a handle open just long enough to break the rename; retry if so
set /a renameAttemptNumber=1
:retryloop
if NOT EXIST %ENV_PATH%\Scripts\python.exe (
set /a renameAttemptNumber+=1
echo Error renaming venv folder; waiting 5 seconds and retrying up to 10x Attempt: %renameAttemptNumber% 
ping -n 6 127.0.0.1 > nul
rename %TMP_ENV_PATH% .xunit-reporter-env
IF %renameAttemptNumber% GEQ 10 GOTO :renamingdone
GOTO :retryloop
)
:renamingdone

echo  %date%-%time%
%ENV_PATH%\Scripts\python.exe -B %~dp0xunit-reporter.py %*
echo  %date%-%time%

set PYTHONPATH=%_OLD_PYTHONPATH%
