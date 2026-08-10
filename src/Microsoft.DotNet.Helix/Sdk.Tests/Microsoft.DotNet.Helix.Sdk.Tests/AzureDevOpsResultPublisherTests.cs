// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Linq;
using System.Net.Http;
using System.Reflection;
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

        [Fact]
        public void FullyQualifiedNames_DataDrivenRowsUseShortChildDisplayNames()
        {
            const string fullyQualifiedName =
                "Microsoft.DotNet.Cli.New.IntegrationTests.CommonTemplatesTests.FeaturesSupport";
            const string dataRowName = "FeaturesSupport(\"classlib\",True,\"netstandard2.0\")";

            using var publisher = new AzureDevOpsResultPublisher(
                new AzureDevOpsReportingParameters(
                    new Uri("https://dev.azure.com/dnceng-public/"),
                    "public",
                    "123",
                    "token",
                    UseFullyQualifiedTestName: true),
                NullLogger.Instance);

            var dataRow = new AggregatedResult(
                AggregationType.Single,
                dataRowName,
                0.1,
                "Passed",
                fullyQualifiedName: fullyQualifiedName);
            var test = new AggregatedResult(
                AggregationType.DataDriven,
                fullyQualifiedName,
                0.1,
                "Passed",
                [dataRow],
                fullyQualifiedName: fullyQualifiedName);

            object publishedTest = ConvertSingleResult(publisher, test);

            Assert.Equal(
                fullyQualifiedName,
                publishedTest.GetType().GetProperty("TestCaseTitle").GetValue(publishedTest));

            object dataRowResult = GetSingleSubResult(publishedTest);

            Assert.Equal(
                dataRowName,
                dataRowResult.GetType().GetProperty("DisplayName").GetValue(dataRowResult));
        }

        [Fact]
        public void FullyQualifiedNames_DataDrivenRerunRowsOnlyShortenDirectChildren()
        {
            const string fullyQualifiedName = "Ns.MyTests.FeaturesSupport";
            const string dataRowName = "FeaturesSupport(\"classlib\")";

            using var publisher = new AzureDevOpsResultPublisher(
                new AzureDevOpsReportingParameters(
                    new Uri("https://dev.azure.com/dnceng-public/"),
                    "public",
                    "123",
                    "token",
                    UseFullyQualifiedTestName: true),
                NullLogger.Instance);

            var attempt = new AggregatedResult(
                AggregationType.Single,
                $"Attempt #1 - {dataRowName}",
                0.1,
                "Passed",
                attemptId: 1,
                fullyQualifiedName: fullyQualifiedName);
            var rerunRow = new AggregatedResult(
                AggregationType.Rerun,
                dataRowName,
                0.1,
                "Passed",
                [attempt],
                fullyQualifiedName: fullyQualifiedName);
            var test = new AggregatedResult(
                AggregationType.DataDriven,
                fullyQualifiedName,
                0.1,
                "Passed",
                [rerunRow],
                fullyQualifiedName: fullyQualifiedName);

            object publishedTest = ConvertSingleResult(publisher, test);
            object publishedRow = GetSingleSubResult(publishedTest);
            object publishedAttempt = GetSingleSubResult(publishedRow);

            Assert.Equal(
                dataRowName,
                publishedRow.GetType().GetProperty("DisplayName").GetValue(publishedRow));
            Assert.Equal(
                $"{fullyQualifiedName} (Attempt #1 - {dataRowName})",
                publishedAttempt.GetType().GetProperty("DisplayName").GetValue(publishedAttempt));
        }

        private static object ConvertSingleResult(
            AzureDevOpsResultPublisher publisher,
            AggregatedResult test)
        {
            MethodInfo convertResults = typeof(AzureDevOpsResultPublisher).GetMethod(
                "ConvertResults",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var convertedResults = Assert.IsAssignableFrom<IEnumerable>(
                convertResults.Invoke(publisher, new object[] { new[] { test }, new object() }));
            object convertedResult = Assert.Single(convertedResults.Cast<object>());

            return convertedResult.GetType().GetProperty("Converted").GetValue(convertedResult);
        }

        private static object GetSingleSubResult(object publishedResult)
        {
            var subResults = Assert.IsAssignableFrom<IEnumerable>(
                publishedResult.GetType().GetProperty("SubResults").GetValue(publishedResult));

            return Assert.Single(subResults.Cast<object>());
        }
    }
}
