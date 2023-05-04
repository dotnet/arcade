// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.DotNet.RemoteExecutor
{
    /// <summary>
    /// Exception thrown when a remote execution times out.
    /// </summary>
    public sealed class RemoteExecutionTimeoutException : RemoteExecutionException
    {
        public RemoteExecutionTimeoutException(string message)
            : base(message, null)
        {
        }
    }
}
