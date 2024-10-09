// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.DotNet.XHarness.Android.Execution;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Android;

public class AdbRunner
{
    private enum AdbProperty
    {
        Architecture,
        ApiVersion,
        SupportedArchitectures,
        InstalledApps,
        BootCompletion,
    }

    #region Constructor and state variables

    private static readonly Dictionary<AdbProperty, string[]> s_commandList = new()
    {
        { AdbProperty.SupportedArchitectures, new[] { "shell", "getprop", "ro.product.cpu.abilist" } },
        { AdbProperty.ApiVersion, new[] { "shell", "getprop", "ro.build.version.sdk" } },
        { AdbProperty.Architecture, new[] { "shell", "getprop", "ro.product.cpu.abi" } },
        { AdbProperty.InstalledApps, new[] { "shell", "pm", "list", "packages", "-3" } },
        { AdbProperty.BootCompletion, new[] { "shell", "getprop", "sys.boot_completed" } },
    };

    private const string AdbEnvironmentVariableName = "ADB_EXE_PATH";
    private const string AdbDeviceFullInstallFailureMessage = "INSTALL_FAILED_INSUFFICIENT_STORAGE";
    private const string AdbInstallBrokenPipeError = "Failure calling service package: Broken pipe";
    private const string AdbInstallException = "Exception occurred while executing 'install':";

    public const string GlobalReadWriteDirectory = "/data/local/tmp";

    private readonly string _absoluteAdbExePath;
    private readonly ILogger _log;
    private readonly IAdbProcessManager _processManager;

    private AndroidDevice? _activeDevice = null;

    public AdbRunner(ILogger log, string adbExePath = "") : this(log, new AdbProcessManager(log), adbExePath) { }

    public AdbRunner(ILogger log, IAdbProcessManager processManager, string adbExePath = "")
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));

        // If we don't get passed one in, use the real implementation
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));

        // We need to find ADB.exe somewhere
        string? environmentPath = Environment.GetEnvironmentVariable(AdbEnvironmentVariableName);
        if (!string.IsNullOrEmpty(environmentPath))
        {
            _log.LogDebug($"Using {AdbEnvironmentVariableName} environment variable ({environmentPath}) for ADB path");
            adbExePath = environmentPath;
        }

        if (string.IsNullOrEmpty(adbExePath))
        {
            adbExePath = GetCliAdbExePath();
        }

        _absoluteAdbExePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(adbExePath));

        if (!File.Exists(_absoluteAdbExePath))
        {
            _log.LogError($"Unable to find adb.exe");
            throw new FileNotFoundException($"Could not find adb.exe. Either set it in the environment via {AdbEnvironmentVariableName} or call with valid path (provided:  '{adbExePath}')", adbExePath);
        }

        if (!_absoluteAdbExePath.Equals(adbExePath))
        {
            _log.LogDebug($"ADBRunner using ADB.exe supplied from {adbExePath}");
            _log.LogDebug($"Full resolved path:'{_absoluteAdbExePath}'");
        }
    }

    private static string GetCliAdbExePath()
    {
        var currentAssemblyDirectory = Path.GetDirectoryName(typeof(AdbRunner).Assembly.Location);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Join(currentAssemblyDirectory, @"..\..\..\runtimes\any\native\adb\windows\adb.exe");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Path.Join(currentAssemblyDirectory, @"../../../runtimes/any/native/adb/linux/adb");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Join(currentAssemblyDirectory, @"../../../runtimes/any/native/adb/macos/adb");
        }
        throw new NotSupportedException("Cannot determine OS platform being used, thus we can not select an ADB executable");
    }

    #endregion

    #region Functions

    public TimeSpan TimeToWaitForBootCompletion { get; set; } = TimeSpan.FromMinutes(5);

    public string GetAdbVersion()
    {
        var result = RunAdbCommand("version");
        result.ThrowIfFailed("Failed to get ADB version");
        return result.StandardOutput;
    }

    public string GetAdbState() => RunAdbCommand("get-state").StandardOutput;

    public string RebootAndroidDevice()
    {
        var result = RunAdbCommand("reboot");
        result.ThrowIfFailed("Failed to reboot the device");
        return result.StandardOutput;
    }

    public void ClearAdbLog()
    {
        RunAdbCommand("logcat", "-b", "all", "-c");

        // Android logs can unnecessarily hide log entries, so disable
        DisableChatty();
    }

    public void EnableWifi(bool enable) => RunAdbCommand("shell", "svc", "wifi", enable ? "enable" : "disable")
        .ThrowIfFailed($"Failed to {(enable ? "enable" : "disable")} WiFi on the device");

    public bool TryDumpAdbLog(string outputFilePath, string filterSpec = "")
    {
        // Workaround: Doesn't seem to have a flush() function and sometimes it doesn't have the full log on emulators.
        Thread.Sleep(3000);

        var result = RunAdbCommand(new[] { "logcat", "-d", filterSpec }, TimeSpan.FromMinutes(2));
        if (result.ExitCode != 0)
        {
            // Could throw here, but it would tear down a possibly otherwise acceptable execution.
            _log.LogError($"Error getting ADB log:{Environment.NewLine}{result}");
            return false;
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath) ?? throw new ArgumentNullException(nameof(outputFilePath)));
            File.WriteAllText(outputFilePath, result.StandardOutput);
            _log.LogInformation($"Wrote current ADB log to {outputFilePath}");
            return true;
        }
    }

    public string DumpBugReport(string outputFilePathWithoutFormat)
    {
        var reportManager = AdbReportFactory.CreateReportManager(_log, GetDeviceApiVersion());
        return reportManager.DumpBugReport(this, outputFilePathWithoutFormat);
    }

    public int GetDeviceApiVersion()
    {
        if (_activeDevice?.ApiVersion != null)
        {
            return _activeDevice.ApiVersion.Value;
        }

        string? output = GetDeviceProperty(AdbProperty.ApiVersion, _activeDevice?.DeviceSerial);

        if (output == null)
        {
            throw new Exception("Failed to get device's API version");
        }

        var apiVersion = int.Parse(output);

        if (_activeDevice != null)
        {
            _activeDevice.ApiVersion = apiVersion;
        }

        return apiVersion;
    }

    public bool WaitForDevice()
    {
        // This command waits for ANY kind of device to be available (emulator or real)
        // Needed because emulators start up asynchronously and take a while.
        // (Returns instantly if device is ready)
        // This can fail if _currentDevice is unset if there are multiple devices.
        _log.LogInformation("Waiting for device to be available (max 5 minutes)");
        RunAdbCommand(new[] { "wait-for-device" }, TimeSpan.FromMinutes(5))
            .ThrowIfFailed("Error waiting for Android device/emulator");

        // Some users will be installing the emulator and immediately calling xharness, they need to be able to expect the device is ready to load APKs.
        // Once wait-for-device returns, we'll give it up to TimeToWaitForBootCompletion (default 5 min) for 'adb shell getprop sys.boot_completed'
        // to be '1' (as opposed to empty) to make subsequent automation happy.
        var watch = Stopwatch.StartNew();
        bool bootCompleted = Retry(
            () =>
            {
                string? result = GetDeviceProperty(AdbProperty.BootCompletion, _activeDevice?.DeviceSerial);
                _log.LogDebug($"sys.boot_completed = '{result}'");
                return result?.StartsWith('1') ?? false;
            },
            retryInterval: TimeSpan.FromSeconds(10),
            retryPeriod: TimeToWaitForBootCompletion);

        if (bootCompleted)
        {
            _log.LogDebug($"Waited {(int)watch.Elapsed.TotalSeconds} seconds for device boot completion");
            return true;
        }
        else
        {
            _log.LogError($"Did not detect boot completion variable on device; device may be in a bad state");
            return false;
        }
    }

    public void StartAdbServer()
    {
        bool started = Retry(
            () =>
            {
                var result = RunAdbCommand(new[] { "start-server" }, TimeSpan.FromMinutes(1));
                started = result.Succeeded;

                if (!started)
                {
                    _log.LogWarning($"Error starting the ADB server" + Environment.NewLine + result);

                    try
                    {
                        KillAdbServer();
                    }
                    catch
                    {
                        _log.LogDebug($"Error killing ADB server after a failed start");
                    }
                }
                else
                {
                    _log.LogDebug(result.StandardOutput);
                }

                return started;
            },
            retryInterval: TimeSpan.FromSeconds(10),
            retryPeriod: TimeSpan.FromMinutes(5));

        if (!started)
        {
            throw new AdbFailureException("Failed to start the ADB server");
        }
    }

    public void KillAdbServer() => RunAdbCommand(new[] { "kill-server" }).ThrowIfFailed("Error killing ADB Server");

    public int CopyHeadlessFolder(string testPath, bool sharedRuntime = false)
    {
        _log.LogInformation($"Attempting to install {testPath}");

        if (string.IsNullOrEmpty(testPath))
        {
            throw new ArgumentException($"No value supplied for {nameof(testPath)} ");
        }

        if (!Directory.Exists(testPath))
        {
            throw new FileNotFoundException($"Could not find {testPath}", testPath);
        }

        var targetDirectory = GlobalReadWriteDirectory + Path.AltDirectorySeparatorChar +  new DirectoryInfo(testPath).Name;
        if (sharedRuntime)
        {
            targetDirectory = GlobalReadWriteDirectory + Path.AltDirectorySeparatorChar +  "runtime";
        }
        var result = RunAdbCommand(new[] { "push", testPath, targetDirectory });


        // Two possible retry scenarios, theoretically both can happen on the same run:

        // 1. Pipe between ADB server and emulator device is broken; restarting the ADB server helps
        if (result.ExitCode == (int)AdbExitCodes.ADB_BROKEN_PIPE || result.StandardError.Contains(AdbInstallBrokenPipeError))
        {
            _log.LogWarning($"Hit broken pipe error; Will make one attempt to restart ADB server, then retry the install");
            KillAdbServer();
            StartAdbServer();
            result = RunAdbCommand(new[] { "push", testPath, targetDirectory });
        }

        // 2. Installation cache on device is messed up; restarting the device reliably seems to unblock this (unless the device is actually full, if so this will error the same)
        if (result.ExitCode != (int)AdbExitCodes.SUCCESS && result.StandardError.Contains(AdbDeviceFullInstallFailureMessage))
        {
            _log.LogWarning($"It seems the package installation cache may be full on the device.  We'll try to reboot it before trying one more time.{Environment.NewLine}Output:{result}");
            RebootAndroidDevice();
            WaitForDevice();
            result = RunAdbCommand(new[] { "push", testPath, targetDirectory });
        }

        // 3. Installation timed out or failed with exception; restarting the ADB server, reboot the device and give more time for installation
        // installer might hang up so we need to clean it up and free memory
        if (result.ExitCode == (int)AdbExitCodes.INSTRUMENTATION_TIMEOUT || (result.ExitCode != (int)AdbExitCodes.SUCCESS && result.StandardError.Contains(AdbInstallException)))
        {
            _log.LogWarning($"Installation failed; Will make one attempt to restart ADB server and the device, then retry the install");
            KillAdbServer();
            StartAdbServer();
            RebootAndroidDevice();
            WaitForDevice();
            result = RunAdbCommand(new[] { "push", testPath, targetDirectory }, TimeSpan.FromMinutes(10));
        }

        if (result.ExitCode != 0)
        {
            _log.LogError($"Error:{Environment.NewLine}{result}");
        }
        else
        {
            _log.LogInformation($"Successfully installed {testPath} to {targetDirectory}");
        }

        return result.ExitCode;
    }

    public int InstallApk(string apkPath)
    {
        _log.LogInformation($"Attempting to install {apkPath}");

        if (string.IsNullOrEmpty(apkPath))
        {
            throw new ArgumentException($"No value supplied for {nameof(apkPath)} ");
        }

        if (!File.Exists(apkPath))
        {
            throw new FileNotFoundException($"Could not find {apkPath}", apkPath);
        }

        var result = RunAdbCommand(new[] { "install", apkPath });

        // Two possible retry scenarios, theoretically both can happen on the same run:

        // 1. Pipe between ADB server and emulator device is broken; restarting the ADB server helps
        if (result.ExitCode == (int)AdbExitCodes.ADB_BROKEN_PIPE || result.StandardError.Contains(AdbInstallBrokenPipeError))
        {
            _log.LogWarning($"Hit broken pipe error; Will make one attempt to restart ADB server, then retry the install");
            KillAdbServer();
            StartAdbServer();
            result = RunAdbCommand(new[] { "install", apkPath });
        }

        // 2. Installation cache on device is messed up; restarting the device reliably seems to unblock this (unless the device is actually full, if so this will error the same)
        if (result.ExitCode != (int)AdbExitCodes.SUCCESS && result.StandardError.Contains(AdbDeviceFullInstallFailureMessage))
        {
            _log.LogWarning($"It seems the package installation cache may be full on the device.  We'll try to reboot it before trying one more time.{Environment.NewLine}Output:{result}");
            RebootAndroidDevice();
            WaitForDevice();
            result = RunAdbCommand(new[] { "install", apkPath });
        }

        // 3. Installation timed out or failed with exception; restarting the ADB server, reboot the device and give more time for installation
        // installer might hang up so we need to clean it up and free memory
        if (result.ExitCode == (int)AdbExitCodes.INSTRUMENTATION_TIMEOUT || (result.ExitCode != (int)AdbExitCodes.SUCCESS && result.StandardError.Contains(AdbInstallException)))
        {
            _log.LogWarning($"Installation failed; Will make one attempt to restart ADB server and the device, then retry the install");
            KillAdbServer();
            StartAdbServer();
            RebootAndroidDevice();
            WaitForDevice();
            result = RunAdbCommand(new[] { "install", apkPath }, TimeSpan.FromMinutes(10));
        }

        if (result.ExitCode != 0)
        {
            _log.LogError($"Error:{Environment.NewLine}{result}");
        }
        else
        {
            _log.LogInformation($"Successfully installed {apkPath}");
        }

        return result.ExitCode;
    }

    public int DeleteHeadlessFolder(string testPath)
    {
        if (string.IsNullOrEmpty(testPath))
        {
            throw new ArgumentNullException(nameof(testPath));
        }

        var fullTestPath = GlobalReadWriteDirectory + Path.AltDirectorySeparatorChar +  new DirectoryInfo(testPath).Name;

        _log.LogInformation($"Attempting to remove folder '{fullTestPath}'..");
        var result = RunAdbCommand(new[] { "shell", "rm", "-fr", fullTestPath });

        // See note above in install()
        if (result.ExitCode == (int)AdbExitCodes.ADB_BROKEN_PIPE)
        {
            _log.LogWarning($"Hit broken pipe error; Will make one attempt to restart ADB server, and retry the uninstallation");

            KillAdbServer();
            StartAdbServer();
            result = RunAdbCommand(new[] { "shell", "rm", "-fr", fullTestPath });
        }

        if (result.ExitCode == (int)AdbExitCodes.SUCCESS)
        {
            _log.LogInformation($"Successfully uninstalled {fullTestPath}");
        }
        else
        {
            _log.LogError(message: $"Failed to uninstall {fullTestPath}: {result}");

        }

        return result.ExitCode;
    }

    public int UninstallApk(string apkName)
    {
        if (string.IsNullOrEmpty(apkName))
        {
            throw new ArgumentNullException(nameof(apkName));
        }

        _log.LogInformation($"Attempting to remove apk '{apkName}'..");
        var result = RunAdbCommand(new[] { "uninstall", apkName });

        // See note above in install()
        if (result.ExitCode == (int)AdbExitCodes.ADB_BROKEN_PIPE)
        {
            _log.LogWarning($"Hit broken pipe error; Will make one attempt to restart ADB server, and retry the uninstallation");

            KillAdbServer();
            StartAdbServer();
            result = RunAdbCommand(new[] { "uninstall", apkName });
        }

        if (result.ExitCode == (int)AdbExitCodes.SUCCESS)
        {
            _log.LogInformation($"Successfully uninstalled {apkName}");
        }
        else if (result.ExitCode == (int)AdbExitCodes.ADB_UNINSTALL_APP_NOT_ON_DEVICE ||
                 result.ExitCode == (int)AdbExitCodes.ADB_UNINSTALL_APP_NOT_ON_EMULATOR)
        {
            _log.LogInformation($"APK '{apkName}' was not on device");
        }
        else
        {
            _log.LogError(message: $"Error: {result}");
        }

        return result.ExitCode;
    }

    // This function works but given we'll likely only be using Instrumentations doesn't matter.
    public int KillApk(string apkName)
    {
        _log.LogInformation($"Killing all running processes for '{apkName}': ");
        var result = RunAdbCommand(new[] { "shell", "am", "kill", "--user", "all", apkName });
        if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
        {
            _log.LogError($"Error:{Environment.NewLine}{result}");
        }
        else
        {
            _log.LogDebug($"Success!{Environment.NewLine}{result.StandardOutput}");
        }
        return result.ExitCode;
    }

    public int KillProcess(string testName)
    {
        _log.LogInformation($"Killing all running processes for '{testName}': ");
        var result = RunAdbCommand(new[] { "shell", "pkill", testName });
        if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
        {
            _log.LogError($"Failed to kill process by name ({testName}):{Environment.NewLine}{result}");

        }
        else
        {
            _log.LogDebug($"Process {testName} killed!{Environment.NewLine}{result.StandardOutput}");

        }
        return result.ExitCode;
    }

    // Assumes the directory is empty so any files present after the pull are new.
    public List<string> PullFiles(string apkPackageName, string devicePath, string localPath)
    {
        if (string.IsNullOrEmpty(localPath))
        {
            throw new ArgumentNullException(nameof(localPath));
        }

        string tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(tempFolder);
            Directory.CreateDirectory(localPath);
            _log.LogInformation($"Attempting to pull contents of {devicePath} to {localPath}");
            var copiedFiles = new List<string>();

            var result = RunAdbCommand(new[] { "pull", devicePath, tempFolder });

            if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
            {
                if (GetDeviceApiVersion() != 30)
                {
                    throw new AdbFailureException($"Failed pulling files: {result}");
                }

                // On Android API 30 we can't use "adb pull" directly due to permission issues on emulators, see https://github.com/dotnet/xharness/issues/385
                // As a workaround we copy the files to the temp directory on the device using "run-as" and pull from there
                _log.LogInformation($"Failed to pull file. Device is running Android API 30, trying fallback to pull {devicePath}");

                result = RunAdbCommand(new[] { "shell", "run-as", apkPackageName, "ls", devicePath });
                result.ThrowIfFailed($"Failed checking for file using fallback: {result}");

                string? fileName = devicePath.Split("/").LastOrDefault();
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    throw new AdbFailureException($"Failed pulling file using fallback: Couldn't determine filename for {devicePath}");
                }

                string deviceTempPath = $"/data/local/tmp/{fileName}";

                result = RunAdbCommand(new[] { "shell", "rm", "-rf", deviceTempPath });
                result.ThrowIfFailed($"Failed removing {deviceTempPath} before using fallback: {result}");

                result = RunAdbCommand(new[] { "shell", "touch", deviceTempPath });
                result.ThrowIfFailed($"Failed touching {deviceTempPath}: {result}");

                result = RunAdbCommand(new[] { "shell", "run-as", apkPackageName, "cp", devicePath, deviceTempPath });
                result.ThrowIfFailed($"Failed copying file using fallback: {result}");

                result = RunAdbCommand(new[] { "pull", deviceTempPath, tempFolder });
                result.ThrowIfFailed($"Failed pulling file using fallback: {result}");

                result = RunAdbCommand(new[] { "shell", "rm", "-f", deviceTempPath });
                result.ThrowIfFailed($"Failed removing {deviceTempPath} after using fallback: {result}");
            }

            var copiedToTemp = Directory.GetFiles(tempFolder, "*", SearchOption.AllDirectories);
            foreach (var filePath in copiedToTemp)
            {
                var relativePath = Path.GetRelativePath(tempFolder, filePath);
                var destinationPath = Path.Combine(localPath, relativePath);
                // if the file is already there, just warn and skip it.
                if (File.Exists(destinationPath))
                {
                    _log.LogWarning($"Skipping file copy as {destinationPath} already exists");
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? throw new ArgumentException(nameof(destinationPath)));
                    File.Move(filePath, destinationPath);
                    copiedFiles.Add(destinationPath);
                }
            }

            _log.LogDebug($"Copied {copiedFiles.Count} files to {localPath}");
            return copiedFiles;
        }
        finally
        {
            Directory.Delete(tempFolder, true);
        }
    }

        // Assumes the directory is empty so any files present after the pull are new.
    public int HeadlessPullFiles(string devicePath, string localPath)
    {
        if (string.IsNullOrEmpty(localPath))
        {
            throw new ArgumentNullException(nameof(localPath));
        }

        Directory.CreateDirectory(localPath);
        _log.LogInformation($"Attempting to pull contents of {devicePath} to {localPath}");

        var result = RunAdbCommand(new[] { "pull", devicePath, localPath });

        if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
        {
            _log.LogError($"Failed to pull file.");
        }
        return (int)AdbExitCodes.SUCCESS;
    }

    /// <summary>
    /// Gets all attached devices and their properties.
    /// </summary>
    public IReadOnlyCollection<AndroidDevice> GetDevices() => GetDevices(
        AdbProperty.Architecture,
        AdbProperty.ApiVersion,
        AdbProperty.SupportedArchitectures);

    /// <summary>
    /// Gets all connected devices that satisfy the requirements.
    /// </summary>
    /// <param name="loadArchitecture">Should we also query device's architecture?</param>
    /// <param name="loadApiVersion">Should we also query device's architecture?</param>
    /// <param name="requiredDeviceId">Specifies a particular device we are looking for</param>
    /// <param name="requiredApiVersion">Filters devices based on the API (SDK) level/version</param>
    /// <param name="requiredArchitectures">Allows only devices that support at least one of given architectures</param>
    /// <param name="requiredInstalledApp">Allows only devices with a given app installed</param>
    /// <returns>List of devices that satisfy the requirements</returns>
    public AndroidDevice? GetDevice(
        bool loadArchitecture = false,
        bool loadApiVersion = false,
        string? requiredDeviceId = null,
        int? requiredApiVersion = null,
        IEnumerable<string>? requiredArchitectures = null,
        string? requiredInstalledApp = null) =>
            GetDevice(
                singleDevice: false,
                loadArchitecture,
                loadApiVersion,
                requiredDeviceId,
                requiredApiVersion,
                requiredArchitectures,
                requiredInstalledApp);

    /// <summary>
    /// Gets all connected devices that satisfy the requirements.
    /// </summary>
    /// <param name="loadArchitecture">Should we also query device's architecture?</param>
    /// <param name="loadApiVersion">Should we also query device's architecture?</param>
    /// <param name="requiredDeviceId">Specifies a particular device we are looking for</param>
    /// <param name="requiredApiVersion">Filters devices based on the API (SDK) level/version</param>
    /// <param name="requiredArchitectures">Allows only devices that support at least one of given architectures</param>
    /// <param name="requiredInstalledApp">Allows only devices with a given app installed</param>
    /// <returns>List of devices that satisfy the requirements</returns>
    public AndroidDevice? GetSingleDevice(
        bool loadArchitecture = false,
        bool loadApiVersion = false,
        string? requiredDeviceId = null,
        int? requiredApiVersion = null,
        IEnumerable<string>? requiredArchitectures = null,
        string? requiredInstalledApp = null) =>
            GetDevice(
                singleDevice: true,
                loadArchitecture,
                loadApiVersion,
                requiredDeviceId,
                requiredApiVersion,
                requiredArchitectures,
                requiredInstalledApp);

    /// <summary>
    /// Gets all connected devices that satisfy the requirements.
    /// </summary>
    /// <param name="requiredDeviceId">Specifies a particular device we are looking for</param>
    /// <param name="requiredApiVersion">Filters devices based on the API (SDK) level/version</param>
    /// <param name="requiredArchitectures">Allows only devices that support at least one of given architectures</param>
    /// <param name="requiredInstalledApp">Allows only devices with a given app installed</param>
    /// <returns>List of devices that satisfy the requirements</returns>
    private IReadOnlyCollection<AndroidDevice> GetAllDevices(
        string? requiredDeviceId = null,
        int? requiredApiVersion = null,
        IEnumerable<string>? requiredArchitectures = null,
        string? requiredInstalledApp = null)
    {
        var properties = new List<AdbProperty>();

        if (requiredApiVersion.HasValue)
        {
            properties.Add(AdbProperty.ApiVersion);
        }

        if (requiredArchitectures?.Any() ?? false)
        {
            properties.Add(AdbProperty.SupportedArchitectures);
        }

        if (requiredInstalledApp != null)
        {
            properties.Add(AdbProperty.InstalledApps);
        }

        IReadOnlyCollection<AndroidDevice> devices;

        try
        {
            devices = GetDevices(properties.ToArray());
        }
        catch (Exception toLog)
        {
            _log.LogError(toLog, $"Exception thrown while trying to find compatible device");
            return Array.Empty<AndroidDevice>();
        }

        if (devices.Count == 0)
        {
            return Array.Empty<AndroidDevice>();
        }

        if (requiredDeviceId != null)
        {
            devices = devices.Where(device => device.DeviceSerial == requiredDeviceId).ToList();

            if (devices.Count == 0)
            {
                _log.LogError($"No attached device with ID {requiredDeviceId} found");
                return devices;
            }
        }

        if (requiredApiVersion != null)
        {
            devices = devices.Where(device => device.ApiVersion == requiredApiVersion).ToList();

            if (devices.Count == 0)
            {
                _log.LogError($"No attached device with API {requiredApiVersion} detected");
                return devices;
            }
        }

        if (requiredArchitectures?.Any() ?? false)
        {
            devices = devices.Where(device => device.SupportedArchitectures?.Intersect(requiredArchitectures).Any() ?? false).ToList();

            if (devices.Count == 0)
            {
                _log.LogError($"No attached device supports one of required architectures {string.Join(", ", requiredArchitectures)}");
                return devices;
            }
        }

        if (requiredInstalledApp != null)
        {
            if (requiredInstalledApp.StartsWith("package:"))
            {
                devices = devices.Where(device => device.InstalledApplications?.Any(app => app.Contains(requiredInstalledApp)) ?? false).ToList();

                if (devices.Count == 0)
                {
                    _log.LogError($"No attached device with app {requiredInstalledApp} installed");
                    return devices;
                }
            }
            else if (requiredInstalledApp.StartsWith("filename:"))
            {
                devices = devices.Where(device => TestFileExists(requiredInstalledApp.Substring("filename:".Length), device.DeviceSerial)).ToList();

                if (devices.Count == 0)
                {
                    _log.LogError($"No attached device with file {requiredInstalledApp} installed");
                    return devices;
                }
            }
            else
            {
                _log.LogError($"Could not understand required app \"{requiredInstalledApp}\"");
            }
        }

        return devices;
    }

    private AndroidDevice? GetDevice(
        bool singleDevice,
        bool loadArchitecture = false,
        bool loadApiVersion = false,
        string? requiredDeviceId = null,
        int? requiredApiVersion = null,
        IEnumerable<string>? requiredArchitectures = null,
        string? requiredInstalledApp = null)
    {
        var devices = GetAllDevices(
            requiredDeviceId,
            requiredApiVersion,
            requiredArchitectures,
            requiredInstalledApp);

        if (devices.Count == 0)
        {
            _log.LogDebug($"No suitable devices found");

            if (singleDevice)
            {
                _log.LogError($"Cannot find a suitable device, please check that a device is attached");
            }

            return null;
        }

        if (singleDevice && devices.Count > 1)
        {
            _log.LogError($"There is more than one suitable device. Please provide API version, device architecture or device ID");
            return null;
        }

        var device = devices.First();
        _log.LogDebug($"Found {devices.Count} possible devices. Using '{device.DeviceSerial}'");

        SetActiveDevice(device);

        if (loadArchitecture && device.Architecture == null)
        {
            device.Architecture = GetDeviceProperty(AdbProperty.Architecture, device.DeviceSerial);
        }

        if (loadApiVersion && device.ApiVersion == null)
        {
            device.ApiVersion = GetDeviceApiVersion();
        }

        return device;
    }

    private IReadOnlyCollection<AndroidDevice> GetDevices(params AdbProperty[] propertiesToLoad)
    {
        string[] standardOutputLines = Array.Empty<string>();

        _log.LogInformation("Finding attached devices/emulators...");

        // Retry up to 3 mins til we get output; if the ADB server isn't started the output will come from a child process and we'll miss it.
        ProcessExecutionResults result = Retry(
            action: () => RunAdbCommand(new[] { "devices", "-l" }, TimeSpan.FromSeconds(30)),
            needsRetry: r =>
            {
                if (!r.Succeeded)
                {
                    _log.LogDebug("Unexpected response from adb devices -l:" + Environment.NewLine + r);
                    return true;
                }

                standardOutputLines = r.StandardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

                // We will keep retrying until we get something back like 'List of devices attached...{newline} {info about a device} ',
                // which when split on newlines ignoring empties will be at least 2 lines when there are any available devices.
                if (standardOutputLines.Length < 2)
                {
                    _log.LogDebug("No attached devices found" + Environment.NewLine + r);
                    return true;
                }

                return false;
            },
            retryInterval: TimeSpan.FromSeconds(10),
            retryPeriod: TimeSpan.FromSeconds(90));

        result.ThrowIfFailed("Failed to enumerate attached devices");

        // Two lines = At least one device was found.  On a multi-device machine, we can't function without specifying device serial number.
        if (standardOutputLines.Length < 2)
        {
            // Abandon the run here, don't just guess.
            _log.LogWarning("No attached devices / emulators detected. " +
                "Check that any emulators have been started, and attached device(s) are connected via USB, powered-on, unlocked and authorized.");
            return Array.Empty<AndroidDevice>();
        }

        var devices = new List<AndroidDevice>();

        _log.LogDebug($"Found {standardOutputLines.Length - 1} possible devices");

        // Start at 1 to skip first line, which is always 'List of devices attached'
        for (int lineNumber = 1; lineNumber < standardOutputLines.Length; lineNumber++)
        {
            _log.LogDebug($"Evaluating output line for device serial: {standardOutputLines[lineNumber]}");
            var lineParts = standardOutputLines[lineNumber].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var device = new AndroidDevice(lineParts[0]);

            foreach (var property in propertiesToLoad)
            {
                string? value = GetDeviceProperty(property, device.DeviceSerial);

                switch (property)
                {
                    case AdbProperty.Architecture:
                        device.Architecture = value;
                        break;

                    case AdbProperty.ApiVersion:
                        device.ApiVersion = value == null ? null : int.Parse(value);
                        break;

                    case AdbProperty.SupportedArchitectures:
                        device.SupportedArchitectures = value?.Split(new char[] { ',', '\r', '\n' });
                        break;

                    case AdbProperty.InstalledApps:
                        device.InstalledApplications = value?.Split("\n");
                        break;
                }
            }

            devices.Add(device);
        }

        return devices;
    }

    private string? GetDeviceProperty(AdbProperty property, string? deviceName = null)
    {
        IEnumerable<string> args = s_commandList[property];

        if (!string.IsNullOrEmpty(deviceName))
        {
            args = new[] { "-s", deviceName }.Concat(args);
        }

        // Assumption: All Devices on a machine running Xharness should attempt to be online or disconnected.
        ProcessExecutionResults result = Retry(
            action: () => RunAdbCommand(args, TimeSpan.FromSeconds(30)),
            needsRetry: r =>
            {
                if (!r.Succeeded || r.StandardError.Contains("device offline", StringComparison.OrdinalIgnoreCase))
                {
                    _log.LogWarning($"Device {deviceName} is offline; retrying up to five minutes");
                    return true;
                }

                return false;
            },
            retryInterval: TimeSpan.FromSeconds(10),
            retryPeriod: TimeSpan.FromMinutes(5));

        if (!result.Succeeded)
        {
            _log.LogError($"Failed to get device's property {property}. Check if a device is attached / emulator is started" +
                Environment.NewLine + result.StandardError);

            return null;
        }

        return result.StandardOutput.Trim();
    }

    private bool TestFileExists(string path, string? deviceName = null)
    {
        var deviceTestPath = GlobalReadWriteDirectory + Path.AltDirectorySeparatorChar + new DirectoryInfo(path).Name;
        IEnumerable<string> args = new string[] {"shell", "stat", deviceTestPath};

        if (!string.IsNullOrEmpty(deviceName))
        {
            args = new[] { "-s", deviceName }.Concat(args);
        }

        // Assumption: All Devices on a machine running Xharness should attempt to be online or disconnected.
        ProcessExecutionResults result = Retry(
            action: () => RunAdbCommand(args, TimeSpan.FromSeconds(30)),
            needsRetry: r =>
            {
                if (r.StandardError.Contains("device offline", StringComparison.OrdinalIgnoreCase))
                {
                    _log.LogWarning($"Device {deviceName} is offline; retrying up to five minutes");
                    return true;
                }

                return false;
            },
            retryInterval: TimeSpan.FromSeconds(10),
            retryPeriod: TimeSpan.FromMinutes(5));

        if (!result.Succeeded)
        {
            _log.LogError($"Failed to check existence of {deviceTestPath}. Check if a device is attached / emulator is started" +
                Environment.NewLine + result.StandardError);

            return false;
        }

        return (result.ExitCode == 0);
    }

    public void SetActiveDevice(AndroidDevice? device)
    {
        _processManager.DeviceSerial = device?.DeviceSerial ?? string.Empty;
        _activeDevice = device;

        if (device is null)
        {
            _log.LogInformation($"Active Android device unset");
        }
        else
        {
            _log.LogInformation($"Active Android device set to serial '{device.DeviceSerial}'");
        }
    }

    public ProcessExecutionResults RunHeadlessCommand(string testPath, string runtimePath, string testAssembly, string testScript, TimeSpan timeout)
    {
        var deviceTestPath = GlobalReadWriteDirectory + Path.AltDirectorySeparatorChar + new DirectoryInfo(testPath).Name + Path.AltDirectorySeparatorChar + testScript;
        var deviceRuntimePath = GlobalReadWriteDirectory + Path.AltDirectorySeparatorChar + "runtime" + Path.AltDirectorySeparatorChar + "dotnet";
        var adbArgs = new List<string>
        {
            "shell",
            deviceTestPath,
            "-r",
            deviceRuntimePath,
        };

        _log.LogInformation($"Setting executable permissions on {testScript} and runtime");
        var result = RunAdbCommand(new[] { "shell", "chmod", "a+x", deviceTestPath, deviceRuntimePath });
        result.ThrowIfFailed($"Failed setting permissions on {deviceTestPath} and {deviceRuntimePath}: {result}");

        _log.LogInformation($"Starting {testScript} from {deviceTestPath} (exit code 0 == success)");


        var stopWatch = Stopwatch.StartNew();
        result = RunAdbCommand(adbArgs, timeout);
        stopWatch.Stop();

        if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
        {
            _log.LogInformation($"An error occurred running {testScript}");
        }
        else
        {
            _log.LogInformation($"Running command {testScript} took {stopWatch.Elapsed.TotalSeconds} seconds");
        }

        _log.LogDebug(result.ToString());

        return result;
    }

    public ProcessExecutionResults RunApkInstrumentation(string apkName, string? instrumentationClassName, Dictionary<string, string> args, TimeSpan timeout)
    {
        string displayName = string.IsNullOrEmpty(instrumentationClassName) ? "{default}" : instrumentationClassName;

        var adbArgs = new List<string>
        {
            "shell", "am", "instrument"
        };

        adbArgs.AddRange(args.SelectMany(arg => new[] { "-e", arg.Key, arg.Value }));
        adbArgs.Add("-w");

        if (string.IsNullOrEmpty(instrumentationClassName))
        {
            _log.LogInformation($"Starting default instrumentation class on {apkName} (exit code 0 == success)");
            adbArgs.Add(apkName);
        }
        else
        {
            _log.LogInformation($"Starting instrumentation class '{instrumentationClassName}' on {apkName}");
            adbArgs.Add($"{apkName}/{instrumentationClassName}");
        }

        var stopWatch = Stopwatch.StartNew();
        var result = RunAdbCommand(adbArgs, timeout);
        stopWatch.Stop();

        if (result.ExitCode == (int)AdbExitCodes.INSTRUMENTATION_TIMEOUT)
        {
            _log.LogWarning("Running instrumentation class {name} timed out after waiting {seconds} seconds", displayName, stopWatch.Elapsed.TotalSeconds);
        }
        else
        {
            _log.LogInformation("Running instrumentation class {name} took {seconds} seconds", displayName, stopWatch.Elapsed.TotalSeconds);
        }

        _log.LogDebug(result.ToString());

        return result;
    }

    private void DisableChatty()
    {
        var result = RunAdbCommand(new[] { "logcat", "-P", "'\"\"'" }, TimeSpan.FromMinutes(1));

        if (!result.Succeeded)
        {
            _log.LogWarning($"Unable to disable chatty. Logcat may hide what it finds to be repeating entries.");
        }
    }

    #endregion

    #region Process runner helpers

    public ProcessExecutionResults RunAdbCommand(params string[] arguments) => RunAdbCommand(arguments, TimeSpan.FromMinutes(5));

    public ProcessExecutionResults RunAdbCommand(IEnumerable<string> arguments, TimeSpan timeOut)
    {
        if (!File.Exists(_absoluteAdbExePath))
        {
            throw new FileNotFoundException($"Provided path for adb.exe was not valid ('{_absoluteAdbExePath}')", _absoluteAdbExePath);
        }

        return _processManager.Run(_absoluteAdbExePath, arguments, timeOut);
    }

    private bool Retry(Func<bool> action, TimeSpan retryInterval, TimeSpan retryPeriod) =>
        Retry(action, result => !result, retryInterval, retryPeriod);

    private T Retry<T>(Func<T> action, Func<T, bool> needsRetry, TimeSpan retryInterval, TimeSpan retryPeriod)
    {
        var watch = Stopwatch.StartNew();
        int attempt = 0;

        T result;
        while (true)
        {
            result = action();

            if (!needsRetry(result))
            {
                return result;
            }

            if (watch.Elapsed > retryPeriod)
            {
                _log.LogDebug($"All {attempt} retries of action failed");
                break;
            }

            ++attempt;
            _log.LogDebug($"Attempt {attempt} failed, retrying in {(int)retryInterval.TotalSeconds} seconds...");
            Thread.Sleep(retryInterval);
        }

        return result;
    }

    #endregion
}
