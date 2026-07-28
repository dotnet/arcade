// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class AzureDevOpsResultPublisherTests
    {
        [Fact]
        public void Constructor_ConfiguresHttpClientTimeoutForLongUploads()
        {
            using var publisher = new AzureDevOpsResultPublisher(
                new AzureDevOpsReportingParameters(
                    new Uri("https://dev.azure.com/dnceng-public/"),
                    "public",
                    "123",
                    "token"),
                NullLogger.Instance);

            FieldInfo field = typeof(AzureDevOpsResultPublisher).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
            var client = Assert.IsType<HttpClient>(field.GetValue(publisher));

            Assert.Equal(TimeSpan.FromMinutes(5), client.Timeout);
        }

        [Theory]
        [InlineData("Passed", true)]
        [InlineData("NotExecuted", true)]
        [InlineData("Inconclusive", true)]
        [InlineData("Failed", false)]
        [InlineData("None", false)]
        public void ComputeAllPassed_SingleResult_OnlyFailedAndNoneCountAsFailure(string result, bool expectedAllPassed)
        {
            var results = new[] { new AggregatedResult(AggregationType.Single, "Test1", 1, result) };

            Assert.Equal(expectedAllPassed, AzureDevOpsResultPublisher.ComputeAllPassed(results));
        }

        [Fact]
        public void ComputeAllPassed_InconclusiveDataDrivenRollup_DoesNotFailTheWorkItem()
        {
            // Mirrors the rollup the aggregator produces for a theory with some passing and some
            // skipped data rows: no data row failed, but the mix isn't a clean pass or skip either.
            var results = new[]
            {
                new AggregatedResult(AggregationType.Single, "Test1", 1, "Passed"),
                new AggregatedResult(AggregationType.DataDriven, "Test2", 1, "Inconclusive"),
            };

            Assert.True(AzureDevOpsResultPublisher.ComputeAllPassed(results));
        }

        [Fact]
        public void ComputeAllPassed_AnyFailedResult_FailsTheWorkItem()
        {
            var results = new[]
            {
                new AggregatedResult(AggregationType.Single, "Test1", 1, "Passed"),
                new AggregatedResult(AggregationType.DataDriven, "Test2", 1, "Failed"),
            };

            Assert.False(AzureDevOpsResultPublisher.ComputeAllPassed(results));
        }

        [Fact]
        public void ReportingParameters_PreserveDefaultAndExplicitWriteRetryBehavior()
        {
            var defaultParameters = new AzureDevOpsReportingParameters(
                CollectionUri: new Uri("https://dev.azure.com/dnceng-public/"),
                TeamProject: "public",
                TestRunId: "123");
            var explicitParameters = new AzureDevOpsReportingParameters(
                new Uri("https://dev.azure.com/dnceng-public/"),
                "public",
                "123",
                AccessToken: null,
                UseFullyQualifiedTestName: false,
                RetryWrites: false);

            Assert.True(defaultParameters.RetryWrites);
            Assert.False(explicitParameters.RetryWrites);
        }

        [Theory]
        [InlineData("", true)]
        [InlineData(@",""RetryWrites"":false", false)]
        public void ReportingParameters_DeserializationPreservesWriteRetryCompatibility(
            string retryWritesJson,
            bool expectedRetryWrites)
        {
            string json = $$"""
                {
                  "CollectionUri": "https://dev.azure.com/dnceng-public/",
                  "TeamProject": "public",
                  "TestRunId": "123"
                  {{retryWritesJson}}
                }
                """;

            AzureDevOpsReportingParameters parameters =
                Assert.IsType<AzureDevOpsReportingParameters>(
                    JsonSerializer.Deserialize<AzureDevOpsReportingParameters>(json));

            Assert.Equal(expectedRetryWrites, parameters.RetryWrites);
        }

        [Fact]
        public async Task RetryHelper_UsesRetryCountPredicateAndExponentialDelays()
        {
            int attempts = 0;
            var delays = new List<TimeSpan>();

            int result = await RetryHelper.RetryAsync(
                () =>
                {
                    attempts++;
                    return attempts < 3
                        ? Task.FromException<int>(new IOException("Transient failure."))
                        : Task.FromResult(42);
                },
                retryCount: 2,
                static ex => ex is IOException,
                onRetry: null,
                CancellationToken.None,
                (delay, _) =>
                {
                    delays.Add(delay);
                    return Task.CompletedTask;
                });

            Assert.Equal(42, result);
            Assert.Equal(3, attempts);
            Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], delays);
        }

        [Fact]
        public async Task RetryHelper_DoesNotRetryMonitorCancellation()
        {
            using var cancellation = new CancellationTokenSource();
            int attempts = 0;
            int delays = 0;

            Task<int> Act()
            {
                attempts++;
                cancellation.Cancel();
                return Task.FromCanceled<int>(cancellation.Token);
            }

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RetryHelper.RetryAsync(
                Act,
                retryCount: 2,
                static ex => ex is TaskCanceledException,
                onRetry: null,
                cancellation.Token,
                (_, _) =>
                {
                    delays++;
                    return Task.CompletedTask;
                }));

            Assert.Equal(1, attempts);
            Assert.Equal(0, delays);
        }
    }
}
