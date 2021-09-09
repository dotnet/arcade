// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Maestro.Client.Models;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public abstract class AssetPublisher : IDisposable
    {
        public TaskLoggingHelper Log { get; }
        public abstract AddAssetLocationToAssetAssetLocationType LocationType { get; }

        protected AssetPublisher(TaskLoggingHelper log)
        {
            Log = log;
        }

        public abstract Task PublishAssetAsync(string file, string blobPath, PushOptions options, SemaphoreSlim clientThrottle = null);

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
