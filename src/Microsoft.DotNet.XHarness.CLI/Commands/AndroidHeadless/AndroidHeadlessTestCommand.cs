// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.AndroidHeadless;

internal class AndroidHeadlessTestCommand : AndroidCommand<AndroidHeadlessTestCommandArguments>
{
    protected override AndroidHeadlessTestCommandArguments Arguments { get; } = new();

    protected override string CommandUsage { get; } = "android-headless test --output-directory=... --test-folder=... --test-command=... [OPTIONS]";

    private const string CommandHelp = "Executes test executable on an Android device, waits up to a given timeout, then copies files off the device and uninstalls the test app";
    protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

    public AndroidHeadlessTestCommand() : base("test", false, CommandHelp)
    {
    }

    protected override ExitCode InvokeCommand(ILogger logger)
    {
        if (!Directory.Exists(Arguments.TestPath))
        {
            logger.LogCritical($"Couldn't find test {Arguments.TestPath}!");
            return ExitCode.PACKAGE_NOT_FOUND;
        }
        if (!Directory.Exists(Arguments.RuntimePath))
        {
            logger.LogCritical($"Couldn't find shared runtime {Arguments.RuntimePath}!");
            return ExitCode.PACKAGE_NOT_FOUND;
        }

        IEnumerable<string> testRequiredArchitecture = Arguments.DeviceArchitecture.Value;

        logger.LogInformation($"Required architecture: '{string.Join("', '", testRequiredArchitecture)}'");

        var runner = new AdbRunner(logger);

        var exitCode = AndroidHeadlessInstallCommand.InvokeHelper(
            logger: logger,
            testPath: Arguments.TestPath,
            runtimePath: Arguments.RuntimePath,
            testRequiredArchitecture: testRequiredArchitecture,
            deviceId: Arguments.DeviceId.Value,
            apiVersion: Arguments.ApiVersion.Value,
            bootTimeoutSeconds: Arguments.LaunchTimeout,
            runner,
            DiagnosticsData);

        if (exitCode == ExitCode.SUCCESS)
        {
            exitCode = AndroidHeadlessRunCommand.InvokeHelper(
                logger: logger,
                testPath: Arguments.TestPath,
                runtimePath: Arguments.RuntimePath,
                testAssembly: Arguments.TestAssembly,
                testScript: Arguments.TestScript,
                outputDirectory: Arguments.OutputDirectory,
                timeout: Arguments.Timeout,
                expectedExitCode: Arguments.ExpectedExitCode,
                wifi: Arguments.Wifi,
                runner: runner);
        }

        runner.DeleteHeadlessFolder(Arguments.TestPath);
        runner.DeleteHeadlessFolder("runtime");
        return exitCode;
    }
}
