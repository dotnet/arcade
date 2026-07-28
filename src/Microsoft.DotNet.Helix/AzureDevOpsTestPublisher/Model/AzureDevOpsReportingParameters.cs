// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Helix.AzureDevOpsTestPublisher.Model;

public sealed record AzureDevOpsReportingParameters(
    Uri CollectionUri,
    string TeamProject,
    string TestRunId,
    string? AccessToken,
    bool UseFullyQualifiedTestName,
    bool RetryWrites)
{
    [JsonConstructor]
    public AzureDevOpsReportingParameters(
        Uri CollectionUri,
        string TeamProject,
        string TestRunId,
        string? AccessToken = null,
        bool UseFullyQualifiedTestName = false)
        : this(CollectionUri, TeamProject, TestRunId, AccessToken, UseFullyQualifiedTestName, RetryWrites: true)
    {
    }

    public void Deconstruct(
        out Uri collectionUri,
        out string teamProject,
        out string testRunId,
        out string? accessToken,
        out bool useFullyQualifiedTestName)
    {
        collectionUri = CollectionUri;
        teamProject = TeamProject;
        testRunId = TestRunId;
        accessToken = AccessToken;
        useFullyQualifiedTestName = UseFullyQualifiedTestName;
    }
}
