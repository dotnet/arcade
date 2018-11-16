
set ENV_PATH=%USERPROFILE%\.vsts-env
echo  %date%-%time%

if NOT EXIST %ENV_PATH%\Scripts\python.exe (
  %HELIX_PYTHONPATH% -m virtualenv --no-site-packages %ENV_PATH%
  %ENV_PATH%\Scripts\python.exe -m pip install vsts==0.1.20
)

echo  %date%-%time%
%ENV_PATH%\Scripts\python.exe %~dp0run.py %*
echo  %date%-%time%
