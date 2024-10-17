// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.XHarness.Common.Logging;

public abstract partial class Log
{
    public static IFileBackedLog CreateReadableAggregatedLog(IFileBackedLog defaultLog, params ILog[] logs) => new ReadableAggregatedLog(defaultLog, logs);

    public static ILog CreateAggregatedLog(params ILog[] logs) => new AggregatedLog(logs);

    // Log that will duplicate log output to multiple other logs.
    private class AggregatedLog : Log
    {
        protected readonly List<ILog> _logs = new();

        public AggregatedLog(params ILog[] logs)
        {
            _logs.AddRange(logs);
            Timestamp = false;
        }

        protected override void WriteImpl(string value)
        {
            foreach (var log in _logs)
            {
                log.Write(value);
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            foreach (var log in _logs)
            {
                log.Write(buffer, offset, count);
            }
        }

        public override void Flush()
        {
            foreach (var log in _logs)
            {
                log.Flush();
            }
        }

        public override void Dispose()
        {
            foreach (var log in _logs)
            {
                log.Dispose();
            }
        }
    }

    private class ReadableAggregatedLog : AggregatedLog, IFileBackedLog
    {
        private readonly IFileBackedLog _defaultLog;

        public ReadableAggregatedLog(IFileBackedLog defaultLog, params ILog[] logs) : base(logs)
        {
            _defaultLog = defaultLog ?? throw new ArgumentNullException(nameof(defaultLog));
            // make sure that we also write in the default log
            _logs.Add(defaultLog);
            Timestamp = false;
        }

        public StreamReader GetReader() => _defaultLog.GetReader();

        public string FullPath => _defaultLog.FullPath;
    }
}
