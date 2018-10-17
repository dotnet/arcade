// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.SignCheck.Logging
{
    public class Log : ILogger
    {
        private List<ILogger> _loggers;

        public bool HasLoggedErrors
        {
            get
            {
                return _loggers.Any(l => l.HasLoggedErrors);
            }
        }

        public LogVerbosity Verbosity
        {
            get;
            private set;
        }

        public Log(string logFile, string errorFile, LogVerbosity verbosity)
        {
            _loggers = new List<ILogger>();
            Add(new FileLogger(verbosity, logFile, errorFile));
            Add(new ConsoleLogger(verbosity));
            Verbosity = verbosity;
        }

        public void Add(ILogger logger)
        {
            _loggers.Add(logger);
        }

        public void WriteMessage(string message)
        {
            _loggers.ForEach(p => p.WriteMessage(message));
        }

        public void WriteMessage(LogVerbosity verbosity, string message)
        {
            _loggers.ForEach(p => p.WriteMessage(verbosity, message));
        }

        public void WriteMessage(LogVerbosity verbosity, string message, params object[] values)
        {
            _loggers.ForEach(p => p.WriteMessage(verbosity, message, values));
        }

        public void WriteError(string message)
        {
            _loggers.ForEach(p => p.WriteError(message));
        }

        public void WriteError(LogVerbosity verbosity, string message)
        {
            _loggers.ForEach(p => p.WriteError(verbosity, message));
        }

        public void WriteError(LogVerbosity verbosity, string message, params object[] values)
        {
            _loggers.ForEach(p => p.WriteError(verbosity, message, values));
        }

        public void WriteLine()
        {
            _loggers.ForEach(p => p.WriteLine());
        }

        public void Close()
        {
            _loggers.ForEach(p => p.Close());
        }
    }
}
