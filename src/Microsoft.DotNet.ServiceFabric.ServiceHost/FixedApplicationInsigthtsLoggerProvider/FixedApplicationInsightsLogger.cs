// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public class FixedApplicationInsightsLogger : ILogger
    {
        private readonly ILogger _inner;
        private readonly TelemetryClient _telemetryClient;

        public FixedApplicationInsightsLogger(ILogger inner, TelemetryClient telemetryClient)
        {
            _inner = inner;
            _telemetryClient = telemetryClient;
        }

        private static Dictionary<string, string> EmptyLogDict { get; } = new Dictionary<string, string>();

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
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

            foreach ((string key, string value) in logDict)
            {
                if (key == "{OriginalFormat}")
                {
                    continue;
                }

                // Fix up the format of key and value to not cause issues until
                // https://github.com/dotnet/corefx/issues/31687 is fixed
                string keyP = Regex.Replace(key, "[^a-zA-Z0-9]", "");
                string valueP = JsonConvert.SerializeObject(value);
                op.AddBaggage(keyP, valueP);
            }

            op.Start();
            _telemetryClient.TrackTrace($"Begin Scope: {logString}", SeverityLevel.Verbose, logDict);
            return new Scope(op, _telemetryClient);
        }

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
