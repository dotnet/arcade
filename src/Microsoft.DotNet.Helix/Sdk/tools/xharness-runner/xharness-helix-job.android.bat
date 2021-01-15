@echo off

REM This script is used as a payload of Helix jobs that execute Android workloads through XHarness on Windows systems.
REM This is used as the entrypoint of the work item so that XHarness failures can be detected and (when appropriate)
REM cause the work item to retry and reboot the Helix agent the work is running on.

REM Currently no special functionality is needed beyond causing infrastructure retry and reboot if the emulators
REM or devices have trouble, but to add more Helix-specific Android XHarness behaviors, this is one extensibility point.

set ADB_DEVICE_ENUMERATION_FAILURE=85

echo Xharness Helix Wrapper: Arguments: %*
%*
set EXIT_CODE=%ERRORLEVEL%

if %EXIT_CODE%==%ADB_DEVICE_ENUMERATION_FAILURE% (
  echo Encountered ADB_DEVICE_ENUMERATION_FAILURE.  This is typically not a failure of the work item.  We will run it again and reboot this computer to help its devices
  echo If this occurs repeatedly, please check for architectural mismatch, e.g. sending x86 or x86_64 APKs to an arm64_v8a-only queue.
  %HELIX_PYTHONPATH% -c "from helix.workitemutil import request_infra_retry; request_infra_retry('Retrying because we could not enumerate all Android devices')"
  %HELIX_PYTHONPATH% -c "from helix.workitemutil import request_reboot; request_reboot('Rebooting to allow Android emulator or device to restart')"
) 

exit /B %EXIT_CODE%
