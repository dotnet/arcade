// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.Commands.Apple.Simulators;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple;

internal class AppleCommandSet : CommandSet
{
    public AppleCommandSet() : base("apple")
    {
        var services = GetAppleDependencies();

        // Commands for full install/execute/uninstall flows
        Add(new AppleTestCommand(services));
        Add(new AppleRunCommand(services));

        // Commands for more fine grained control over the separate operations
        Add(new AppleInstallCommand(services));
        Add(new AppleUninstallCommand(services));
        Add(new AppleJustTestCommand(services));
        Add(new AppleJustRunCommand(services));

        // Commands for getting information
        Add(new AppleDeviceCommand(services));
        Add(new AppleMlaunchCommand(services));
        Add(new AppleStateCommand());

        // Commands for simulator management
        Add(new SimulatorsCommandSet());
    }

    public static IServiceCollection GetAppleDependencies()
    {
        var services = new ServiceCollection();

        services.TryAddSingleton<IAppBundleInformationParser, AppBundleInformationParser>();
        services.TryAddSingleton<ISimulatorLoader, SimulatorLoader>();
        services.TryAddSingleton<IHardwareDeviceLoader, HardwareDeviceLoader>();
        services.TryAddSingleton<IDeviceFinder, DeviceFinder>();
        services.TryAddSingleton<IiOSExitCodeDetector, iOSExitCodeDetector>();
        services.TryAddSingleton<IMacCatalystExitCodeDetector, MacCatalystExitCodeDetector>();
        services.TryAddSingleton<IHelpers, Helpers>();

        services.TryAddTransient<IErrorKnowledgeBase, ErrorKnowledgeBase>();
        services.TryAddTransient<ICaptureLogFactory, CaptureLogFactory>();
        services.TryAddTransient<IDeviceLogCapturerFactory, DeviceLogCapturerFactory>();
        services.TryAddTransient<ICrashSnapshotReporterFactory, CrashSnapshotReporterFactory>();
        services.TryAddTransient<ITestReporterFactory, TestReporterFactory>();
        services.TryAddTransient<IResultParser, XmlResultParser>();

        services.TryAddTransient<IAppInstaller, AppInstaller>();
        services.TryAddTransient<IAppTester, AppTester>();
        services.TryAddTransient<IAppRunner, AppRunner>();
        services.TryAddTransient<IAppUninstaller, AppUninstaller>();
        services.TryAddTransient<IAppTesterFactory, AppTesterFactory>();
        services.TryAddTransient<IAppRunnerFactory, AppRunnerFactory>();

        services.TryAddTransient<IInstallOrchestrator, InstallOrchestrator>();
        services.TryAddTransient<IJustRunOrchestrator, JustRunOrchestrator>();
        services.TryAddTransient<IJustTestOrchestrator, JustTestOrchestrator>();
        services.TryAddTransient<IRunOrchestrator, RunOrchestrator>();
        services.TryAddTransient<ITestOrchestrator, TestOrchestrator>();
        services.TryAddTransient<IUninstallOrchestrator, UninstallOrchestrator>();
        services.TryAddTransient<ISimulatorResetOrchestrator, SimulatorResetOrchestrator>();

        return services;
    }
}
