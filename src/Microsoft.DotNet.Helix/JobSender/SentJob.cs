// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client.Models;

namespace Microsoft.DotNet.Helix.Client
{
    internal class SentJob : ISentJob
    {
        public SentJob(IJob jobApi, JobCreationResult newJob)
        {
            JobApi = jobApi;
            CorrelationId = newJob.Name;
            HelixCancellationToken = newJob.CancellationToken;
        }

        public IJob JobApi { get; }
        public string CorrelationId { get; }
        public string HelixCancellationToken { get; }

        public async Task<JobPassFail> WaitAsync(int pollingIntervalMs = 10000, CancellationToken cancellationToken = default)
        {
            try
            {
                return await JobApi.WaitForJobAsync(CorrelationId, pollingIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (!string.IsNullOrEmpty(HelixCancellationToken))
                {
                    try
                    {
                        await JobApi.CancelAsync(CorrelationId, HelixCancellationToken);
                    }
                    catch
                    {
                        // Best-effort cancellation; don't mask the original cancellation exception
                    }
                }
                throw;
            }
        }
    }
}
