// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher.Model;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class LocalTestResultsReaderTests
    {
        [Fact]
        public async Task PackingTestReporter_CanUnpackFromSpecifiedDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            string originalDirectory = Environment.CurrentDirectory;

            try
            {
                Environment.CurrentDirectory = tempDirectory;

                var reporter = new PackingTestReporter(
                    new AzureDevOpsReportingParameters(new Uri("https://dev.azure.com/dnceng/"), "arcade", "42"));

                await reporter.ReportResultsAsync(
                [
                    new TestResult(
                        "Sample.Tests.Passes",
                        "unit",
                        "Sample.Tests",
                        "Passes",
                        1.25,
                        "Pass",
                        null,
                        null,
                        null,
                        null)
                ]);

                var unpacked = await PackingTestReporter.UnpackResultsAsync(tempDirectory);

                Assert.True(unpacked.HasValue);
                Assert.Equal("42", unpacked.Value.Parameters.TestRunId);
                Assert.Single(unpacked.Value.Results);
                Assert.Equal("Sample.Tests.Passes", unpacked.Value.Results[0].Name);
            }
            finally
            {
                Environment.CurrentDirectory = originalDirectory;
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void LocalTestResultsReader_ReadsXunitFileFromDownloadedResults()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string workItemDirectory = Path.Combine(tempDirectory, "work-item");
            Directory.CreateDirectory(workItemDirectory);

            try
            {
                File.WriteAllText(
                    Path.Combine(workItemDirectory, "testResults.xml"),
                    """
                    <assemblies>
                      <assembly name="Sample.Tests.dll" total="1" passed="1" failed="0" skipped="0">
                        <collection total="1" passed="1" failed="0" skipped="0">
                          <test name="Sample.Tests.Passes" type="Sample.Tests" method="Passes" time="0.125" result="Pass" />
                        </collection>
                      </assembly>
                    </assemblies>
                    """);

                var reader = new LocalTestResultsReader();
                var resultSets = reader.ReadResults(tempDirectory);
                var aggregate = new ResultAggregator().Aggregate(resultSets);
                AggregatedResult result = Assert.Single(aggregate);

                Assert.Equal("Sample.Tests.Passes", result.Name);
                Assert.Equal("Passed", result.Result);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public async Task LocalTestResultsReader_CombinesPackedAndXmlResultsAcrossWorkItems()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string packedDirectory = Path.Combine(tempDirectory, "packed-item");
            string xmlDirectory = Path.Combine(tempDirectory, "xml-item");
            Directory.CreateDirectory(packedDirectory);
            Directory.CreateDirectory(xmlDirectory);
            string originalDirectory = Environment.CurrentDirectory;

            try
            {
                Environment.CurrentDirectory = packedDirectory;
                var reporter = new PackingTestReporter(
                    new AzureDevOpsReportingParameters(new Uri("https://dev.azure.com/dnceng/"), "arcade", "42"));
                await reporter.ReportResultsAsync(
                [
                    new TestResult("Packed.Tests.Passes", "unit", "Packed.Tests", "Passes", 1, "Pass", null, null, null, null)
                ]);

                File.WriteAllText(
                    Path.Combine(xmlDirectory, "testResults.xml"),
                    """
                    <assemblies>
                      <assembly name="Xml.Tests.dll" total="1" passed="1" failed="0" skipped="0">
                        <collection total="1" passed="1" failed="0" skipped="0">
                          <test name="Xml.Tests.Passes" type="Xml.Tests" method="Passes" time="0.250" result="Pass" />
                        </collection>
                      </assembly>
                    </assemblies>
                    """);

                var resultSets = new LocalTestResultsReader().ReadResults(tempDirectory);
                var aggregate = new ResultAggregator().Aggregate(resultSets);

                Assert.Equal(2, aggregate.Count);
                Assert.Contains(aggregate, static x => x.Name == "Packed.Tests.Passes");
                Assert.Contains(aggregate, static x => x.Name == "Xml.Tests.Passes");
            }
            finally
            {
                Environment.CurrentDirectory = originalDirectory;
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
