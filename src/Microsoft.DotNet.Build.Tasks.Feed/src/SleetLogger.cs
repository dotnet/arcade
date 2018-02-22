// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Common;
using System.Threading.Tasks;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class SleetLogger : ILogger
    {
        private MSBuild.TaskLoggingHelper _log;

        public SleetLogger(MSBuild.TaskLoggingHelper log)
        {
            _log = log;
        }
        public void Log(LogLevel level, string data)
        {
            _log.LogMessage(data, level);
        }

        public void Log(ILogMessage message)
        {
            _log.LogMessage(message.Message, message.Level);
        }

        public Task LogAsync(LogLevel level, string data)
        {
            return Task.Run(() => _log.LogMessage(data, level));
        }

        public Task LogAsync(ILogMessage message)
        {
            return Task.Run(() => _log.LogMessage(message.Message, message.Level));
        }

        public void LogDebug(string data)
        {
            _log.LogMessage(data, LogLevel.Debug);
        }

        public void LogError(string data)
        {
            // There are cases where Sleet fails and we retry on our side causing things to actually work but if Sleet logs an error the whole build leg
            // is marked as failed even though it actually succeeded, hence we log a warning here but we will log an error if the retry did not help
            _log.LogWarning($"This error is being logged as a warning: {data}");
        }

        public void LogInformation(string data)
        {
            _log.LogMessage(data, LogLevel.Information);
        }

        public void LogInformationSummary(string data)
        {
            _log.LogMessage(data, LogLevel.Information);
        }

        public void LogMinimal(string data)
        {
            _log.LogMessage(data, LogLevel.Minimal);
        }

        public void LogVerbose(string data)
        {
            _log.LogMessage(data, LogLevel.Verbose);
        }

        public void LogWarning(string data)
        {
            _log.LogWarning(data);
        }
    }
}
