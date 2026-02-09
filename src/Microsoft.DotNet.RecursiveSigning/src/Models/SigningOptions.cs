// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Options for the signing process.
    /// </summary>
    public sealed class SigningOptions
    {
        /// <summary>
        /// Maximum degree of parallelism for signing operations.
        /// </summary>
        public int MaxDegreeOfParallelism { get; }

        public SigningOptions(int maxDegreeOfParallelism = 1)
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism;
        }
    }
}
