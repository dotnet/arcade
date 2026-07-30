// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Helix.JobMonitor;
using Microsoft.DotNet.Helix.JobMonitor.Models;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class JobLineageTests
    {
        public static TheoryData<string, bool, int> StageAttempts => new()
        {
            { null, false, 1 },
            { "", false, 1 },
            { "malformed", true, 1 },
            { "999999999999999999999", true, 1 },
            { "1", true, 1 },
            { " 2 ", true, 2 },
            { "0", true, 0 },
            { "-1", true, -1 },
        };

        [Theory]
        [MemberData(nameof(StageAttempts))]
        public void StageAttempt_PreservesPresenceAndExistingNumericFallback(
            string value,
            bool expectedIsSpecified,
            int expectedNumber)
        {
            StageAttempt attempt = StageAttempt.Parse(value);

            Assert.Equal(value, attempt.Value);
            Assert.Equal(expectedIsSpecified, attempt.IsSpecified);
            Assert.Equal(expectedNumber, attempt.Number);
        }

        [Fact]
        public void StageAttempt_ValueComparisonIsOrdinalIgnoreCase()
        {
            Assert.True(StageAttempt.Parse("MALFORMED").HasSameValue(StageAttempt.Parse("malformed")));
            Assert.False(StageAttempt.Parse("01").HasSameValue(StageAttempt.Parse("1")));
        }

        public static IEnumerable<object[]> WorkStreamIdentities()
        {
            yield return
            [
                new[] { Job("job", submitter: "Test_Linux", queue: "Ubuntu") },
                "job",
                "submitter:Test_Linux|queue:Ubuntu",
            ];
            yield return
            [
                new[] { Job("job", submitter: "Test_Linux") },
                "job",
                "submitter:Test_Linux",
            ];
            yield return
            [
                new[]
                {
                    Job("retry", previous: "original"),
                    Job("original", submitter: "Test_Linux", queue: "Ubuntu"),
                },
                "retry",
                "submitter:Test_Linux|queue:Ubuntu",
            ];
            yield return
            [
                new[]
                {
                    Job("retry", previous: "original"),
                    Job("original"),
                },
                "retry",
                "helix:original",
            ];
            yield return
            [
                new[] { Job("retry", previous: "missing") },
                "retry",
                "helix:missing",
            ];
            yield return
            [
                new[]
                {
                    Job("cycle-a", previous: "cycle-b"),
                    Job("cycle-b", previous: "cycle-a"),
                },
                "cycle-a",
                "helix:cycle-a",
            ];
        }

        [Theory]
        [MemberData(nameof(WorkStreamIdentities))]
        public void WorkStreamIdentity_UsesOnlyTheProvidedSnapshot(
            HelixJobInfo[] jobs,
            string selectedJobName,
            string expectedIdentity)
        {
            var lineage = new JobLineage(jobs);
            HelixJobInfo selected = jobs.Single(job => job.JobName == selectedJobName);

            Assert.Equal(expectedIdentity, lineage.GetIncarnation(selected).WorkStream.ToString());
        }

        [Fact]
        public void WorkStreamIdentity_IsCaseInsensitiveAndQueueSensitive()
        {
            WorkStreamIdentity upper = WorkStreamIdentity.FromSubmitter("Test_Linux", "Ubuntu");
            WorkStreamIdentity lower = WorkStreamIdentity.FromSubmitter("test_linux", "ubuntu");
            WorkStreamIdentity differentQueue = WorkStreamIdentity.FromSubmitter("test_linux", "osx");

            Assert.Equal(upper, lower);
            Assert.NotEqual(upper, differentQueue);
        }

        public static IEnumerable<object[]> LatestIncarnations()
        {
            yield return
            [
                new[]
                {
                    Job("retry", previous: "ORIGINAL"),
                    Job("original"),
                },
                new[] { "retry" },
            ];
            yield return
            [
                new[]
                {
                    Job("zzz-old", submitter: "Test", queue: "q", attempt: "1"),
                    Job("aaa-new", submitter: "Test", queue: "q", attempt: "2"),
                },
                new[] { "aaa-new" },
            ];
            yield return
            [
                new[]
                {
                    Job("malformed", submitter: "Test", queue: "q", attempt: "not-a-number"),
                    Job("higher", submitter: "Test", queue: "q", attempt: "2"),
                },
                new[] { "higher" },
            ];
            yield return
            [
                new[]
                {
                    Job("ubuntu", submitter: "Test", queue: "ubuntu", attempt: "2"),
                    Job("osx", submitter: "Test", queue: "osx", attempt: "1"),
                },
                new[] { "osx", "ubuntu" },
            ];
            yield return
            [
                new[]
                {
                    Job("missing-a", previous: "unknown-a"),
                    Job("missing-b", previous: "unknown-b"),
                },
                new[] { "missing-a", "missing-b" },
            ];
            yield return
            [
                new[]
                {
                    Job("cycle-a", previous: "cycle-b"),
                    Job("cycle-b", previous: "cycle-a"),
                },
                Array.Empty<string>(),
            ];
        }

        [Theory]
        [MemberData(nameof(LatestIncarnations))]
        public void GetLatestIncarnationsPerStream_HandlesLinkedUnlinkedAndIncompleteLineage(
            HelixJobInfo[] jobs,
            string[] expectedJobNames)
        {
            string[] actual = new JobLineage(jobs)
                .GetLatestIncarnationsPerStream()
                .Select(incarnation => incarnation.Job.JobName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            string[] reversed = new JobLineage(jobs.Reverse())
                .GetLatestIncarnationsPerStream()
                .Select(incarnation => incarnation.Job.JobName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expectedJobNames.OrderBy(name => name, StringComparer.Ordinal), actual);
            Assert.Equal(expectedJobNames.OrderBy(name => name, StringComparer.Ordinal), reversed);
        }

        public static IEnumerable<object[]> OldToNewOrderings()
        {
            yield return
            [
                new[]
                {
                    Job("third", previous: "second"),
                    Job("first"),
                    Job("second", previous: "first"),
                },
                new[] { "first", "second", "third" },
            ];
            yield return
            [
                new[]
                {
                    Job("aaa-new", submitter: "Test", queue: "q", attempt: "2"),
                    Job("zzz-old", submitter: "Test", queue: "q", attempt: "1"),
                },
                new[] { "zzz-old", "aaa-new" },
            ];
            yield return
            [
                new[]
                {
                    Job("z-malformed", attempt: "bad"),
                    Job("a-missing"),
                    Job("higher", attempt: "2"),
                },
                new[] { "a-missing", "z-malformed", "higher" },
            ];
            yield return
            [
                new[]
                {
                    Job("b-incomplete", previous: "missing"),
                    Job("a-root"),
                },
                new[] { "a-root", "b-incomplete" },
            ];
            yield return
            [
                new[]
                {
                    Job("cycle-b", previous: "cycle-a"),
                    Job("cycle-a", previous: "cycle-b"),
                },
                new[] { "cycle-a", "cycle-b" },
            ];
        }

        [Theory]
        [MemberData(nameof(OldToNewOrderings))]
        public void OrderOldToNew_IsDeterministicForMalformedAndCyclicLineage(
            HelixJobInfo[] jobs,
            string[] expectedJobNames)
        {
            string[] actual = new JobLineage(jobs)
                .OrderOldToNew()
                .Select(incarnation => incarnation.Job.JobName)
                .ToArray();
            string[] reversed = new JobLineage(jobs.Reverse())
                .OrderOldToNew()
                .Select(incarnation => incarnation.Job.JobName)
                .ToArray();

            Assert.Equal(expectedJobNames, actual);
            Assert.Equal(expectedJobNames, reversed);
        }

        private static HelixJobInfo Job(
            string name,
            string submitter = null,
            string queue = null,
            string previous = null,
            string attempt = null)
            => new(
                name,
                status: "finished",
                submitterJobName: submitter,
                queueId: queue,
                previousHelixJobName: previous,
                stageAttempt: attempt);
    }
}
