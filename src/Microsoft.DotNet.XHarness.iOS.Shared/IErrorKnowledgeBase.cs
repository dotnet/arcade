// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.XHarness.Common.Logging;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared;

/// <summary>
/// Interface to be implemented to determine if error in the installation, build and execution of the tests
/// are known. This class will help users understand better why an error ocurred.
/// </summary>
public interface IErrorKnowledgeBase
{
    /// <summary>
    /// Identifies via the logs if the installation failure is due to a known issue that the user can act upon.
    /// </summary>
    /// <param name="installLog">The installation log.</param>
    /// <param name="knownFailureMessage">A string message for the user to understand the reason for the failure.</param>
    /// <returns>True if the failure is due to a known reason, false otherwise.</returns>
    bool IsKnownInstallIssue(IFileBackedLog installLog, [NotNullWhen(true)] out KnownIssue? knownFailureMessage);

    /// <summary>
    /// Identifies via the logs if the build failure is due to a known issue that the user can act upon.
    /// </summary>
    /// <param name="buildLog">The build log.</param>
    /// <param name="knownFailureMessage">A string message for the user to understand the reason for the failure.</param>
    /// <returns>True if the failure is due to a known reason, false otherwise.</returns>
    bool IsKnownBuildIssue(IFileBackedLog buildLog, [NotNullWhen(true)] out KnownIssue? knownFailureMessage);

    /// <summary>
    /// Identifies via the logs if the run failure is due to a known issue that the user can act upon.
    /// </summary>
    /// <param name="runLog">The run log.</param>
    /// <param name="knownFailureMessage">A string message for the user to understand the reason for the failure.</param>
    /// <returns>True if the failure is due to a known reason, false otherwise.</returns>
    bool IsKnownTestIssue(IFileBackedLog runLog, [NotNullWhen(true)] out KnownIssue? knownFailureMessage);
}

public class KnownIssue
{
    /// <summary>
    /// Human readable message that can be presented to the user.
    /// </summary>
    public string HumanMessage { get; }

    /// <summary>
    /// Link to an issue where this problem is being handled.
    /// </summary>
    public string? IssueLink { get; }

    /// <summary>
    /// Suggested exit code 
    /// </summary>
    public int? SuggestedExitCode { get; }

    public KnownIssue(string humanMessage, string? issueLink = null, int? suggestedExitCode = null)
    {
        HumanMessage = humanMessage;
        IssueLink = issueLink;
        SuggestedExitCode = suggestedExitCode;
    }
}
