// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.AspNetCore.Logging;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    // Fix for app insights issue https://github.com/Microsoft/ApplicationInsights-aspnetcore/issues/491
    public static class FixedApplicationInsightsLoggerFactoryExtensions
    {
        public static ILoggingBuilder AddFixedApplicationInsights(this ILoggingBuilder builder,
            LogLevel minLevel)
        {
            return builder.AddFixedApplicationInsights((category, logLevel) => logLevel >= minLevel);
        }

        public static ILoggingBuilder AddFixedApplicationInsights(this ILoggingBuilder builder, Func<string, LogLevel, bool> filter)
        {
            builder.Services.AddSingleton<ILoggerProvider>(
                provider => new FixedApplicationInsightsLoggerProvider(
                    provider.GetRequiredService<TelemetryClient>(),
                    filter,
                    provider.GetRequiredService<IOptions<ApplicationInsightsLoggerOptions>>()));
            return builder;
        }
    }

    [ProviderAlias("FixedApplicationInsights")]
    public class FixedApplicationInsightsLoggerProvider : ILoggerProvider
    {
        private readonly ILoggerProvider _inner;
        private readonly TelemetryClient _telemetryClient;

        public FixedApplicationInsightsLoggerProvider(TelemetryClient telemetryClient, Func<string, LogLevel, bool> filter, IOptions<ApplicationInsightsLoggerOptions> options)
        {
            _telemetryClient = telemetryClient;
            // OFC the ApplicationInsights stuff is all internal so we can't inherit any of it

            var appInsightsLoggerProviderTypeName =
                $"Microsoft.ApplicationInsights.AspNetCore.Logging.ApplicationInsightsLoggerProvider, {typeof(ApplicationInsightsLoggerFactoryExtensions).Assembly.FullName}";
            var appInsightsLoggerProviderType = Type.GetType(appInsightsLoggerProviderTypeName);
            if (appInsightsLoggerProviderType == null)
            {
                throw new TypeLoadException($"Could not load type {appInsightsLoggerProviderTypeName}");
            }
            _inner = (ILoggerProvider)Activator.CreateInstance(appInsightsLoggerProviderType, telemetryClient, filter, options);
        }

        public void Dispose()
        {
            _inner.Dispose();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FixedApplicationInsightsLogger(_inner.CreateLogger(categoryName), _telemetryClient);
        }
    }

    public class FixedApplicationInsightsLogger : ILogger
    {
        private readonly ILogger _inner;
        private readonly TelemetryClient _telemetryClient;

        public FixedApplicationInsightsLogger(ILogger inner, TelemetryClient telemetryClient)
        {
            _inner = inner;
            _telemetryClient = telemetryClient;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            _inner.Log(logLevel, eventId, state, exception, formatter);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _inner.IsEnabled(logLevel);
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            string logString = state.ToString();
            var op = new Activity(logString);
            Dictionary<string, string> logDict;

            if (state is IEnumerable<KeyValuePair<string, object>> enumerable)
            {
                logDict = enumerable.ToDictionary(p => p.Key, p => Convert.ToString(p.Value));
            }
            else
            {
                logDict = EmptyLogDict;
            }

            foreach (var (key, value) in logDict)
            {
                op.AddBaggage(key, value);
            }

            op.Start();
            _telemetryClient.TrackTrace($"Begin Scope: {logString}", SeverityLevel.Information, logDict);
            return new Scope(op, _telemetryClient);
        }

        private static Dictionary<string, string> EmptyLogDict { get; } = new Dictionary<string, string>();

        private class Scope : IDisposable
        {
            private readonly Activity _op;
            private readonly TelemetryClient _telemetryClient;

            public Scope(Activity op, TelemetryClient telemetryClient)
            {
                _op = op;
                _telemetryClient = telemetryClient;
            }

            public void Dispose()
            {
                _telemetryClient.TrackTrace($"End Scope: {_op.OperationName}");
                _op.Stop();
            }
        }
    }
}
