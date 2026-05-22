// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client.Models;

namespace Microsoft.DotNet.Helix.Client
{
    /// <summary>
    /// Job that has been already sent to Helix, but is not
    /// necessarily evaluated yet by its agents.
    /// </summary>
    public interface ISentJob
    {
        /// <summary>
        /// The ID of the job assigned by Helix.
        /// </summary>
        string CorrelationId { get; }

        /// <summary>
        /// Token allowing cancellation of this specific job without other credentials
        /// </summary>
        string HelixCancellationToken { get; }

        /// <summary>
        /// Poll for the job to actually finish inside Helix.
        /// If the <paramref name="cancellationToken"/> is cancelled while waiting, this method will attempt
        /// to cancel the in-flight Helix job using <see cref="HelixCancellationToken"/> before re-throwing
        /// the cancellation exception.
        /// </summary>
        Task<JobPassFail> WaitAsync(int pollingIntervalMs = 10000, CancellationToken cancellationToken = default);
    }
}
