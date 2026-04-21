// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Helix.AzureDevOpsTestReporter.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Helix.AzureDevOpsTestReporter;

public sealed record AzureDevOpsReportingParameters(Uri CollectionUri, string TeamProject, string TestRunId, string? AccessToken = null);

public static class LoggerFactoryExtensions
{
    public static ILogger OrNull(this ILogger? logger)
    {
        return logger ?? NullLogger.Instance;
    }
}
