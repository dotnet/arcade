// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Options;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    internal abstract class Operation : IDisposable
    {
        protected ILoggerFactory _loggerFactory;
        private ILogger _logger;

        protected ILogger Logger { get { return _logger; } }

        public Operation(CommandLineOptions options)
        {
            // Because the internal logging in DarcLib tends to be chatty and non-useful,
            // we remap the --verbose switch onto 'info', --debug onto highest level, and the
            // default level onto warning
            LogLevel level = LogLevel.Warning;
            if (options.Debug)
            {
                level = LogLevel.Debug;
            }
            else if (options.Verbose)
            {
                level = LogLevel.Information;
            }
            _loggerFactory = new LoggerFactory().AddConsole(level);
            _logger = _loggerFactory.CreateLogger<Operation>();
        }

        public abstract Task<int> ExecuteAsync();

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _loggerFactory.Dispose();
                }

                disposedValue = true;
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
