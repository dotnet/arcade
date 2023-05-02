// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.DotNet.RemoteExecutor
{
    public sealed class RemoteExecutionException : Exception
    {
        private readonly string? _stackTrace;
        public RemoteExecutionException(string? stackTrace)
            : base("Remote process failed with an unhandled exception.")
        {
            _stackTrace = stackTrace;
        }

        public override string? StackTrace => _stackTrace ?? base.StackTrace;
    }
}
