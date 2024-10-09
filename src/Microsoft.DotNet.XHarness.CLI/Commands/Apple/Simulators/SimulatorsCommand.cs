// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple.Simulators;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple.Simulators;

internal abstract class SimulatorsCommand : XHarnessCommand<SimulatorsCommandArguments>
{
    private const string MAJOR_VERSION_PLACEHOLDER = "DOWNLOADABLE_VERSION_MAJOR";
    private const string MINOR_VERSION_PLACEHOLDER = "DOWNLOADABLE_VERSION_MINOR";
    private const string VERSION_PLACEHOLDER = "DOWNLOADABLE_VERSION";
    private const string IDENTIFIER_PLACEHOLDER = "DOWNLOADABLE_IDENTIFIER";

    protected const string SimulatorHelpString =
        "Accepts a list of simulator IDs to install. The ID can be a fully qualified string, " +
        "e.g. com.apple.pkg.AppleTVSimulatorSDK14_2 or you can use the format in which you specify " +
        "apple targets for XHarness tests (ios-simulator, tvos-simulator, watchos-simulator, xros-simulator).";

    private static readonly HttpClient s_client = new(new HttpClientHandler { CheckCertificateRevocationList = true });
    private readonly MacOSProcessManager _processManager = new();
    private string? _xcodeVersion;
    private string? _xcodeUuid;

    protected ILogger Logger { get; set; } = null!;

    protected SimulatorsCommand(string name, bool allowsExtraArgs, string help)
        : base(TargetPlatform.Apple, name, allowsExtraArgs, new ServiceCollection(), help)
    {
    }

    protected static string TempDirectory
    {
        get
        {
            var path = Path.Combine(Path.GetTempPath(), "simulator-installer");

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }
    }

    protected async Task<(bool Succeeded, string Stdout)> ExecuteCommand(
        string filename,
        TimeSpan? timeout = null,
        params string[] arguments)
    {
        var stdoutLog = new MemoryLog() { Timestamp = false };
        var stderrLog = new MemoryLog() { Timestamp = false };

        var result = await _processManager.ExecuteCommandAsync(
            filename,
            arguments,
            new CallbackLog(m => Logger.LogDebug(m)),
            stdoutLog,
            stderrLog,
            timeout ?? TimeSpan.FromSeconds(30));

        var stderr = stderrLog.ToString();
        if (stderr.Length > 0)
        {
            Logger.LogDebug("Error output:" + Environment.NewLine + stderr);
        }

        return (result.Succeeded, stdoutLog.ToString());
    }

    protected async Task<IEnumerable<Simulator>> GetAvailableSimulators()
    {

        var doc = new XmlDocument();
        doc.LoadXml(await GetSimulatorIndexXml() ?? throw new FailedToGetIndexException());

        var simulators = new List<Simulator>();

        var downloadables = doc.SelectNodes("//plist/dict/key[text()='downloadables']/following-sibling::array[1]/dict");
        foreach (XmlNode? downloadable in downloadables!)
        {
            if (downloadable == null)
            {
                continue;
            }

            var nameNode = downloadable.SelectSingleNode("key[text()='name']/following-sibling::string") ?? throw new Exception("Name node not found");
            var versionNode = downloadable.SelectSingleNode("key[text()='version']/following-sibling::string") ?? throw new Exception("Version node not found");
            var identifierNode = downloadable.SelectSingleNode("key[text()='identifier']/following-sibling::string") ?? throw new Exception("Identifier node not found");
            var sourceNode = downloadable.SelectSingleNode("key[text()='source']/following-sibling::string");

            var fileSizeNode = downloadable.SelectSingleNode("key[text()='fileSize']/following-sibling::integer|key[text()='fileSize']/following-sibling::real");
            var installPrefixNode = downloadable.SelectSingleNode("key[text()='userInfo']/following-sibling::dict/key[text()='InstallPrefix']/following-sibling::string");

            var version = versionNode.InnerText;
            var versions = version.Split('.');
            var versionMajor = versions[0];
            var versionMinor = versions[1];
            var dict = new Dictionary<string, string>() {
                    { MAJOR_VERSION_PLACEHOLDER, versionMajor },
                    { MINOR_VERSION_PLACEHOLDER, versionMinor },
                    { VERSION_PLACEHOLDER, version },
                };

            var identifier = ReplaceStringUsingKey(identifierNode.InnerText, dict);

            dict.Add(IDENTIFIER_PLACEHOLDER, identifier);

            _ = double.TryParse(fileSizeNode?.InnerText, out var parsedFileSize);

            var name = ReplaceStringUsingKey(nameNode.InnerText, dict);
            var installPrefix = ReplaceStringUsingKey(installPrefixNode?.InnerText, dict);
            if (installPrefix is null)
            {
                // newer simulators aren't installed anymore, provide a dummy value here
                var simRuntimeName = name.Replace(" Simulator", ".simruntime");
                installPrefix = $"/Library/Developer/CoreSimulator/Profiles/Runtimes/{simRuntimeName}";
            }

            var platform = name.Split(' ').FirstOrDefault();
            if (platform is null)
            {
                Logger.LogWarning($"Platform name could not be parsed from simulator name: '{nameNode.InnerText}' version: '{versionNode.InnerText}' identifier: '{identifierNode.InnerText}' skipping...");
                continue;
            }

            var source = ReplaceStringUsingKey(sourceNode?.InnerText, dict);
            var isCryptexDiskImage = false;
            if (source is null)
            {
                // We allow source to be missing for newer simulators (e.g., iOS 18+ available from Xcode 16) that use cryptographically-sealed archives.
                // Eg.:
                // <dict>
                //     <key>category</key>
                //     <string>simulator</string>
                //     <key>contentType</key>
                //     <string>cryptexDiskImage</string>
                //     ...
                // These images are downloaded and installed through xcodebuild instead.
                // https://developer.apple.com/documentation/xcode/installing-additional-simulator-runtimes#Install-and-manage-Simulator-runtimes-from-the-command-line
                var contentTypeNode = downloadable.SelectSingleNode("key[text()='contentType']/following-sibling::string") ?? throw new Exception("ContentType node not found");
                var contentType = contentTypeNode.InnerText;
                if (contentType.Equals("cryptexDiskImage", StringComparison.OrdinalIgnoreCase))
                {
                    isCryptexDiskImage = true;
                    Logger.LogInformation($"Simulator with name: '{nameNode.InnerText}' version: '{versionNode.InnerText}' identifier: '{identifierNode.InnerText}' has no source but it is a cryptex disk image which can be downloaded through xcodebuild.");
                }
                else
                {
                    Logger.LogWarning($"Simulator with name: '{nameNode.InnerText}' version: '{versionNode.InnerText}' identifier: '{identifierNode.InnerText}' has no source for download nor it is a cryptex disk image, skipping...");
                    continue;
                }
            }

            simulators.Add(new Simulator(
                name: name,
                platform: platform,
                identifier: ReplaceStringUsingKey(identifierNode.InnerText, dict),
                version: versionNode.InnerText,
                source: source,
                installPrefix: installPrefix,
                fileSize: (long)parsedFileSize,
                isCryptexDiskImage: isCryptexDiskImage
                ));
        }

        return simulators;
    }

    [return: NotNullIfNotNull("value")]
    static string? ReplaceStringUsingKey(string? value, Dictionary<string, string> replacements)
    {
        if (value is null)
            return null;

        foreach (var kvp in replacements)
        {
            value = value.Replace($"$({kvp.Key})", kvp.Value);
        }

        return value;
    }

    protected async Task<Version?> IsInstalled(Simulator simulator)
    {
        string xcodeVersionString = await GetXcodeVersion();
        bool isXcode14 = Version.TryParse(xcodeVersionString, out var xcodeVersion) && xcodeVersion.Major >= 14;

        if (simulator.Identifier.StartsWith("com.apple.dmg.") && isXcode14)
        {
            var (succeeded, json) = await ExecuteCommand($"xcrun", TimeSpan.FromMinutes(1), "simctl", "runtime", "list", "-j");
            if (!succeeded)
            {
                return null;
            }
            Logger.LogDebug($"Listing runtime disk images via returned: {json}");

            string simulatorRuntime = "";
            string simulatorVersion = "";

            if (simulator.Identifier.StartsWith("com.apple.dmg.iPhoneSimulatorSDK")) {
                simulatorRuntime = "com.apple.CoreSimulator.SimRuntime.iOS-";
                simulatorVersion = simulator.Identifier.Substring("com.apple.dmg.iPhoneSimulatorSDK".Length);
            }
            else if (simulator.Identifier.StartsWith("com.apple.dmg.AppleTVSimulatorSDK")) {
                simulatorRuntime = "com.apple.CoreSimulator.SimRuntime.tvOS-";
                simulatorVersion = simulator.Identifier.Substring("com.apple.dmg.AppleTVSimulatorSDK".Length);
            }
            else if (simulator.Identifier.StartsWith("com.apple.dmg.WatchSimulatorSDK")) {
                simulatorRuntime = "com.apple.CoreSimulator.SimRuntime.watchOS-";
                simulatorVersion = simulator.Identifier.Substring("com.apple.dmg.WatchSimulatorSDK".Length);
            }
            else if (simulator.Identifier.StartsWith("com.apple.dmg.xrSimulatorSDK")) {
                simulatorRuntime = "com.apple.CoreSimulator.SimRuntime.xrOS-";
                simulatorVersion = simulator.Identifier.Substring("com.apple.dmg.xrSimulatorSDK".Length);
            }
            else {
                Logger.LogWarning($"Unknown simulator type: {simulator.Identifier}");
            }

            // trim away any beta suffix
            string simulatorBetaVersion = "";
            if (simulatorVersion.Contains("_b")) {
                simulatorBetaVersion = simulatorVersion.Substring(simulatorVersion.LastIndexOf("_b") + "_b".Length);
                simulatorVersion = simulatorVersion.Substring(0, simulatorVersion.LastIndexOf("_b"));
            }

            var runtimeIdentifier = simulatorRuntime + simulatorVersion.Replace('_', '-');
            var simulators = JsonDocument.Parse(json);

            foreach(JsonProperty sim in simulators.RootElement.EnumerateObject())
            {
                if (sim.Value.GetProperty("runtimeIdentifier").GetString() == runtimeIdentifier)
                { 
                    var version = sim.Value.GetProperty("version").GetString();
                    if (version == null)
                        return null;

                    // make sure we have a proper major.minor.build.revision version
                    // and if we have a beta version, add it to the version as the revision parameter
                    if (version.Count(c => c == '.') == 1)
                        version += simulatorBetaVersion == "" ? ".0.0" : $".0.{simulatorBetaVersion}";
                    else if (version.Count(c => c == '.') == 2)
                        version += simulatorBetaVersion == "" ? ".0" : $".{simulatorBetaVersion}";

                    // TODO: the version returned by simctl and index2.dvtdownloadableindex for dmg packages is not a unique version like for pkg but just major.minor.0.0,
                    // we could use the "build" key from simctl to compare with the "buildUpdate" in the index2.dvtdownloadableindex

                    return Version.TryParse(version, out var parsedVersion) ? parsedVersion : null;
                }
            }

            return null;
        }
        else if (simulator.Identifier.StartsWith("com.apple.pkg."))
        {
            var (succeeded, pkgInfo) = await ExecuteCommand($"pkgutil", TimeSpan.FromMinutes(1), "--pkg-info", simulator.Identifier);
            if (!succeeded)
            {
                return null;
            }

            var lines = pkgInfo.Split('\n');
            var version = lines.First(v => v.StartsWith("version: ", StringComparison.Ordinal)).Substring("version: ".Length);
            return Version.Parse(version);
        }

        return null;
    }

    protected IEnumerable<string> ParseSimulatorIds()
    {
        var simulators = new List<string>();

        foreach (string argument in ExtraArguments)
        {
            if (argument.StartsWith("com.apple.pkg.") || argument.StartsWith("com.apple.dmg."))
            {
                simulators.Add(argument);
                continue;
            }

            TestTargetOs target;
            try
            {
                target = argument.ParseAsAppRunnerTargetOs();
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ArgumentException(
                    $"Failed to parse simulator '{argument}'. Available values are ios-simulator, tvos-simulator, watchos-simulator and xros-simulator." +
                    Environment.NewLine + Environment.NewLine +
                    "You need to also specify the version. Example: ios-simulator_13.4");
            }

            if (string.IsNullOrEmpty(target.OSVersion))
            {
                throw new ArgumentException($"Failed to parse simulator '{argument}'. " +
                    $"You need to specify the exact version. Example: ios-simulator_13.4");
            }

            var testTargetVersion = Version.Parse(target.OSVersion);

            (string simulatorName, string simulatorFormat) = target.Platform switch
            {
                TestTarget.Simulator_iOS64 => ("iPhone", testTargetVersion.Major >= 16 ? "dmg" : "pkg"),
                TestTarget.Simulator_tvOS => ("AppleTV", testTargetVersion.Major >= 16 ? "dmg" : "pkg"),
                TestTarget.Simulator_watchOS => ("Watch", testTargetVersion.Major >= 9 ? "dmg" : "pkg"),
                TestTarget.Simulator_xrOS => ("xrOS", "dmg"),
                _ => throw new ArgumentException($"Failed to parse simulator '{argument}'. " +
                    "Available values are ios-simulator, tvos-simulator, watchos-simulator and xros-simulator." +
                    Environment.NewLine + Environment.NewLine +
                    "You need to also specify the version. Example: ios-simulator_13.4"),
            };

            // e.g. com.apple.pkg.AppleTVSimulatorSDK14_3
            simulators.Add($"com.apple.{simulatorFormat}.{simulatorName}SimulatorSDK{target.OSVersion.Replace(".", "_")}");
        }

        return simulators;
    }

    private async Task<string?> GetSimulatorIndexXml()
    {
        var xcodeVersion = await GetXcodeVersion();
        string indexUrl, indexName;

        if (Version.Parse(xcodeVersion).Major >= 14)
        {
            /*
            * The following url was found while debugging Xcode, the "index2" part is actually hardcoded:
            * 
            *	DVTFoundation`-[DVTDownloadableIndexSource identifier]:
            *		0x103db478d <+0>:  pushq  %rbp
            *		0x103db478e <+1>:  movq   %rsp, %rbp
            *		0x103db4791 <+4>:  leaq   0x53f008(%rip), %rax      ; @"index2"
            *		0x103db4798 <+11>: popq   %rbp
            *		0x103db4799 <+12>: retq
            * 
            */
            indexName = $"index-{xcodeVersion}.dvtdownloadableindex";
            indexUrl = "https://devimages-cdn.apple.com/downloads/xcode/simulators/index2.dvtdownloadableindex";
        }
        else
        {
            var xcodeUuid = await GetXcodeUuid();
            indexName = $"index-{xcodeVersion}-{xcodeUuid}.dvtdownloadableindex";

            indexUrl = $"https://devimages-cdn.apple.com/downloads/xcode/simulators/{indexName}";
        }

        var tmpfile = Path.Combine(TempDirectory, indexName);
        if (!File.Exists(tmpfile))
        {
            if (!await DownloadFile(indexUrl, tmpfile))
                return null;
        }
        else
        {
            Logger.LogInformation($"File '{tmpfile}' already exists, skipped download");
        }

        var (succeeded, xmlResult) = await ExecuteCommand("plutil", TimeSpan.FromSeconds(30), "-convert", "xml1", "-o", "-", tmpfile);
        if (!succeeded)
        {
            return null;
        }

        return xmlResult;
    }

    private async Task<bool> DownloadFile(string url, string destinationPath)
    {
        try
        {
            Logger.LogInformation($"Downloading {url}...");

            var downloadTask = s_client.GetStreamAsync(url);
            using var fileStream = new FileStream(destinationPath, FileMode.Create);
            using var bodyStream = await downloadTask;
            await bodyStream.CopyToAsync(fileStream);
            return true;
        }
        catch (HttpRequestException e)
        {
            // 403 means 404
            if (e.StatusCode == HttpStatusCode.Forbidden)
            {
                // Apple's servers return a 403 if the file doesn't exist, which can be quite confusing, so show a better error.
                Logger.LogWarning($"Failed to download {url}: Not found");
            }
            else
            {
                Logger.LogWarning($"Failed to download {url}: {e}");
            }
        }

        return false;
    }

    protected async Task<string> GetXcodeVersion()
    {
        if (_xcodeVersion is not null)
        {
            return _xcodeVersion;
        }

        string xcodeRoot = Arguments.XcodeRoot.Value ?? new MacOSProcessManager().XcodeRoot;
        var plistPath = Path.Combine(xcodeRoot, "Contents", "Info.plist");

        var (succeeded, xcodeVersion) = await ExecuteCommand("/usr/libexec/PlistBuddy", TimeSpan.FromSeconds(5), "-c", "Print :DTXcode", plistPath);
        if (!succeeded)
        {
            throw new Exception("Failed to detect Xcode version!");
        }

        xcodeVersion = xcodeVersion.Trim();

        // the first two digits of DTXcode are the major version, then minor and revision so e.g. 1520 would translate to 15.2.0
        xcodeVersion = xcodeVersion.Insert(xcodeVersion.Length - 2, ".");
        xcodeVersion = xcodeVersion.Insert(xcodeVersion.Length - 1, ".");

        _xcodeVersion = xcodeVersion;

        return _xcodeVersion;
    }

    private async Task<string> GetXcodeUuid()
    {
        if (_xcodeUuid is not null)
        {
            return _xcodeUuid;
        }

        string xcodeRoot = Arguments.XcodeRoot.Value ?? new MacOSProcessManager().XcodeRoot;
        var plistPath = Path.Combine(xcodeRoot, "Contents", "Info.plist");

        var (succeeded, xcodeUuid) = await ExecuteCommand("/usr/libexec/PlistBuddy", TimeSpan.FromSeconds(5), "-c", "Print :DVTPlugInCompatibilityUUID", plistPath);
        if (!succeeded)
        {
            throw new Exception("Failed to detect Xcode UUID! This is only available on Xcode < 15.3.");
        }

        _xcodeUuid = xcodeUuid.Trim();

        return _xcodeUuid;
    }

    [Serializable]
    protected class FailedToGetIndexException : Exception
    {
        public FailedToGetIndexException() : this("Failed to download the list of available simulators from Apple")
        {
        }

        public FailedToGetIndexException(string? message) : base(message)
        {
        }

        public FailedToGetIndexException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
