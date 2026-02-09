// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Abstractions
{
    /// <summary>
    /// Main coordinator for the recursive signing workflow.
    /// </summary>
    public interface IRecursiveSigning
    {
        /// <summary>
        /// Execute the complete signing workflow: discovery → iterative signing → finalization.
        /// </summary>
        /// <param name="request">Signing request with input files and configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Signing result with success status, signed files, and errors.</returns>
        Task<SigningResult> SignAsync(SigningRequest request, CancellationToken cancellationToken = default);
    }
}
