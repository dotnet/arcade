// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests;

public class QueueStatsLoggingTests
{
    [Theory]
    [InlineData(true, null, null, null)]
    [InlineData(true, "legacy-token", null, null)]
    [InlineData(true, null, "creator", "Creator is forbidden when using authenticated access.")]
    [InlineData(false, "legacy-token", "creator", "Creator is forbidden when using authenticated access.")]
    [InlineData(false, null, null, "Creator is required when using anonymous access.")]
    [InlineData(false, null, "creator", null)]
    public void CreatorValidationRecognizesEntraAsAuthenticated(
        bool useEntraAuthentication,
        string accessToken,
        string creator,
        string expectedError)
    {
        Assert.Equal(
            expectedError,
            SendHelixJob.GetCreatorValidationError(
                useEntraAuthentication,
                accessToken,
                creator));
    }

    // Exercises the callback pair SendHelixJob hands to JobDefinition.SendAsync. Routine
    // submission progress must always stay at Normal; the opt-in queue-health summary must be
    // elevated to High only when EnableShowHelixQueueStats is set, so it survives the default
    // 'Minimal' build verbosity without turning routine output into noise.
    private static List<(string Message, MessageImportance Importance)> CaptureLogs(
        bool enableShowHelixQueueStats)
    {
        var captured = new List<(string, MessageImportance)>();
        var (logNormal, logQueueStats) = SendHelixJob.CreateSubmissionLoggers(
            enableShowHelixQueueStats,
            (msg, importance) => captured.Add((msg, importance)));

        logNormal("submitting payload");
        logQueueStats("Helix queue 'test' health:");
        return captured;
    }

    [Fact]
    public void QueueStatsSummary_LogsAtHigh_WhenEnabled()
    {
        var captured = CaptureLogs(enableShowHelixQueueStats: true);

        Assert.Equal(MessageImportance.Normal, captured[0].Importance);
        Assert.Equal(MessageImportance.High, captured[1].Importance);
    }

    [Fact]
    public void QueueStatsSummary_StaysAtNormal_WhenDisabled()
    {
        var captured = CaptureLogs(enableShowHelixQueueStats: false);

        Assert.Equal(MessageImportance.Normal, captured[0].Importance);
        Assert.Equal(MessageImportance.Normal, captured[1].Importance);
    }

    [Fact]
    public void CancellationTokenAttachmentContainsJobScopedSecret()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            string path = SendHelixJob.WriteCancellationTokenAttachment(
                "job-id",
                "cancellation-token",
                directory);

            JObject payload = JObject.Parse(File.ReadAllText(path));
            Assert.Equal("job-id", payload.Value<string>("jobName"));
            Assert.Equal("cancellation-token", payload.Value<string>("cancellationToken"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
