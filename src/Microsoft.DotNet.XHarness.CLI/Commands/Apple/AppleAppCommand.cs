// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple;

internal abstract class AppleAppCommand<TArguments> : AppleCommand<TArguments> where TArguments : IAppleAppRunArguments
{
    protected readonly ErrorKnowledgeBase ErrorKnowledgeBase = new();

    protected AppleAppCommand(string name, bool allowsExtraArgs, IServiceCollection services, string? help = null)
        : base(name, allowsExtraArgs, services, help)
    {
    }

    protected sealed override async Task<ExitCode> Invoke(Extensions.Logging.ILogger logger)
    {
        var targetName = Arguments.Target.Value.AsString();

        logger.LogInformation("Preparing run for {target}", targetName + (!string.IsNullOrEmpty(Arguments.DeviceName.Value) ? " targeting " + Arguments.DeviceName.Value : null));

        // Create main log file for the run
        using ILogs logs = new Logs(Arguments.OutputDirectory);
        string logFileName = $"{Name}-{targetName}{(!string.IsNullOrEmpty(Arguments.DeviceName.Value) ? "-" + Arguments.DeviceName.Value : null)}.log";
        IFileBackedLog runLog = logs.Create(logFileName, LogType.ExecutionLog.ToString(), timestamp: true);

        // Pipe the execution log to the debug output of XHarness effectively making "-v" turn this on
        CallbackLog debugLog = new(message => logger.LogDebug(message.Trim()));
        using var mainLog = Log.CreateReadableAggregatedLog(runLog, debugLog);

        Services.TryAddSingleton(logs);
        Services.TryAddTransient<XHarness.Apple.ILogger, ConsoleLogger>();

        Services.TryAddSingleton(mainLog);
        Services.TryAddSingleton<ILog>(mainLog);
        Services.TryAddSingleton<IReadableLog>(mainLog);

        var serviceProvider = Services.BuildServiceProvider();

        var diagnosticsData = serviceProvider.GetRequiredService<IDiagnosticsData>();
        diagnosticsData.Target = Arguments.Target.Value.AsString();
        diagnosticsData.IsDevice = !Arguments.Target.Value.Platform.IsSimulator();

        var cts = new CancellationTokenSource();
        cts.CancelAfter(Arguments.Timeout);
        cts.Token.Register(() =>
        {
            logger.LogError("Run timed out after {timeout} seconds", Math.Ceiling(Arguments.Timeout.Value.TotalSeconds));
        });

        return await InvokeInternal(serviceProvider, cts.Token);
    }

    protected abstract Task<ExitCode> InvokeInternal(ServiceProvider serviceProvider, CancellationToken cancellationToken);

    [SuppressMessage("Usage", "CA2254:The logging message template should not vary between calls to LoggerExtensions", Justification = "This is just a simple shim")]
    protected class ConsoleLogger : XHarness.Apple.ILogger
    {
        private readonly Extensions.Logging.ILogger _logger;

        public ConsoleLogger(Extensions.Logging.ILogger logger)
        {
            _logger = logger;
        }

        public void LogDebug(string message) => _logger.LogDebug(message);
        public void LogInformation(string message) => _logger.LogInformation(message);
        public void LogWarning(string message) => _logger.LogWarning(message);
        public void LogError(string message) => _logger.LogError(message);
        public void LogCritical(string message) => _logger.LogCritical(message);
    }
}
