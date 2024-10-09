// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands;

public abstract class XHarnessCommand<T> : Command where T : IXHarnessCommandArguments
{
    private readonly TargetPlatform _targetPlatform;
    private readonly bool _allowsExtraArgs;

    /// <summary>
    /// Will be printed in the header when help is invoked.
    /// Example: 'ios package [OPTIONS]'
    /// </summary>
    protected abstract string CommandUsage { get; }

    /// <summary>
    /// Will be printed in the header when help is invoked.
    /// Example: 'Allows to package DLLs into an app bundle'
    /// </summary>
    protected abstract string CommandDescription { get; }

    protected abstract T Arguments { get; }

    /// <summary>
    /// Service collection used to create dependencies.
    /// </summary>
    protected IServiceCollection Services { get; }

    protected XHarnessCommand(
        TargetPlatform targetPlatform,
        string name,
        bool allowsExtraArgs,
        IServiceCollection services,
        string? help = null)
        : base(name, help)
    {
        _allowsExtraArgs = allowsExtraArgs;
        _targetPlatform = targetPlatform;
        Services = services;
    }

    /// <summary>
    /// Contains all arguments after the verbatim "--" argument.
    ///
    /// Example:
    ///   > xharness ios test --arg1=value1 -- --foo -v
    ///   Will contain [ "foo", "v" ]
    /// </summary>
    protected IEnumerable<string> PassThroughArguments { get; private set; } = Enumerable.Empty<string>();

    /// <summary>
    /// Extra arguments parsed to the command (if the command allows it).
    /// </summary>
    protected IEnumerable<string> ExtraArguments { get; private set; } = Enumerable.Empty<string>();

    public sealed override int Invoke(IEnumerable<string> arguments)
    {
        var commandArguments = Arguments.GetCommandArguments();
        var options = new OptionSet();

        foreach (var arg in commandArguments)
        {
            options.Add(arg.Prototype, arg.Description, arg.Action);
        }

        using var parseFactory = CreateLoggerFactory(Arguments.Verbosity);
        var parseLogger = parseFactory.CreateLogger(Name);

        try
        {
            var regularArguments = arguments.TakeWhile(arg => arg != Program.VerbatimArgumentPlaceholder);
            if (regularArguments.Count() < arguments.Count())
            {
                PassThroughArguments = arguments.Skip(regularArguments.Count() + 1);
                arguments = regularArguments;
            }

            var extra = options.Parse(arguments);

            if (extra.Count > 0)
            {
                if (_allowsExtraArgs)
                {
                    ExtraArguments = extra;
                }
                else
                {
                    throw new ArgumentException($"Unknown arguments: {string.Join(" ", extra)}");
                }
            }

            if (Arguments.ShowHelp)
            {
                Console.WriteLine("usage: " + CommandUsage + Environment.NewLine + Environment.NewLine + CommandDescription + Environment.NewLine);
                options.WriteOptionDescriptions(Console.Out);
                return (int)ExitCode.HELP_SHOWN;
            }

            Arguments.Validate();
        }
        catch (ArgumentException e)
        {
            parseLogger.LogError(e.Message);

            if (Arguments.ShowHelp)
            {
                options.WriteOptionDescriptions(Console.Out);
            }

            return (int)ExitCode.INVALID_ARGUMENTS;
        }
        catch (Exception e)
        {
            parseLogger.LogCritical("Unexpected failure argument: {error}", e);
            return (int)ExitCode.GENERAL_FAILURE;
        }

        // Should we and where output the diagnostics data?
        string? diagnosticsPath = Arguments.Diagnostics.Value ?? Environment.GetEnvironmentVariable(EnvironmentVariables.Names.DIAGNOSTICS_PATH);

        try
        {
            using var factory = CreateLoggerFactory(Arguments.Verbosity);
            ILogger logger = factory.CreateLogger(Name);
            var diagnostics = new CommandDiagnostics(logger, _targetPlatform, Name);

            Services.TryAddSingleton(logger);
            Services.TryAddSingleton<IDiagnosticsData>(diagnostics);

            var result = InvokeInternal(logger).GetAwaiter().GetResult();

            diagnostics.ExitCode = result;

            // Save diagnostic data into a file
            if (result != ExitCode.HELP_SHOWN && !string.IsNullOrEmpty(diagnosticsPath))
            {
                diagnostics.SaveToJsonFile(diagnosticsPath);
            }

            return (int)result;
        }
        catch (Exception e)
        {
            parseLogger.LogCritical(e.ToString());
            return (int)ExitCode.GENERAL_FAILURE;
        }
    }

    protected abstract Task<ExitCode> InvokeInternal(ILogger logger);

    private static ILoggerFactory CreateLoggerFactory(LogLevel verbosity) => LoggerFactory.Create(builder =>
    {
        builder
            .AddConsoleFormatter<XHarnessConsoleLoggerFormatter, SimpleConsoleFormatterOptions>(options =>
            {
                options.ColorBehavior = EnvironmentVariables.IsTrue(EnvironmentVariables.Names.DISABLE_COLOR_OUTPUT) ? LoggerColorBehavior.Disabled : LoggerColorBehavior.Default;
                options.TimestampFormat = EnvironmentVariables.IsTrue(EnvironmentVariables.Names.LOG_TIMESTAMPS) ? "[HH:mm:ss] " : null!;
            })
            .AddConsole(options => options.FormatterName = XHarnessConsoleLoggerFormatter.FormatterName)
            .SetMinimumLevel(verbosity);
    });
}
