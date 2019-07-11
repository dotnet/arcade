
set ENV_PATH=%USERPROFILE%\.vsts-env
echo  %date%-%time%

if NOT EXIST %ENV_PATH%\Scripts\python.exe (
  %HELIX_PYTHONPATH% -m virtualenv --no-site-packages %ENV_PATH%
)

%ENV_PATH%\Scripts\python.exe -c "import azure-devops" || %ENV_PATH%\Scripts\python.exe -m pip install azure-devops==5.0.0b9

%ENV_PATH%\Scripts\python.exe -c "import future" || %ENV_PATH%\Scripts\python.exe -m pip install future==0.17.1

echo  %date%-%time%
%ENV_PATH%\Scripts\python.exe %~dp0run.py %*
echo  %date%-%time%
