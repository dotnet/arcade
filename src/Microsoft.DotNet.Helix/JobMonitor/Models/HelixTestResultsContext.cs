// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Helix.JobMonitor
{
    public sealed record HelixTestResultsContext(
        string JobName,
        string OutputDirectory,
        string ResultsSas);
}
