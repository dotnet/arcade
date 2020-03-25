
set ENV_PATH=%USERPROFILE%\.azdo-env
set TMP_ENV_PATH=%USERPROFILE%\.azdo-env-tmp

REM Removing pythonpath forces a clean installation of the Azure DevOps client, but subsequent commands may use HELIX libraries
set _OLD_PYTHONPATH=%PYTHONPATH%
set PYTHONPATH=

echo  %date%-%time%

if NOT EXIST %ENV_PATH%\Scripts\python.exe (
  rmdir /Q /S %TMP_ENV_PATH%
  rmdir /Q /S %ENV_PATH%
  %HELIX_PYTHONPATH% -m virtualenv --no-site-packages %TMP_ENV_PATH%
  rename %TMP_ENV_PATH% .azdo-env
)
REM On certain slow machines python.exe keeps a handle open just long enough to break the rename; retry if so
set /a renameAttemptNumber=1
:retryloop
if NOT EXIST %ENV_PATH%\Scripts\python.exe (
set /a renameAttemptNumber+=1
echo Error renaming venv folder; waiting 5 seconds and retrying up to 10x Attempt: %renameAttemptNumber% 
ping -n 6 127.0.0.1 > nul
rename %TMP_ENV_PATH% .azdo-env
IF %renameAttemptNumber% GEQ 10 GOTO :renamingdone
GOTO :retryloop
)
:renamingdone

%ENV_PATH%\Scripts\python.exe -c "import azure.devops" || %ENV_PATH%\Scripts\python.exe -m pip install azure-devops==5.0.0b9

%ENV_PATH%\Scripts\python.exe -c "import future" || %ENV_PATH%\Scripts\python.exe -m pip install future==0.17.1

echo  %date%-%time%
%ENV_PATH%\Scripts\python.exe -B %~dp0run.py %*
echo  %date%-%time%

set PYTHONPATH=%_OLD_PYTHONPATH%
