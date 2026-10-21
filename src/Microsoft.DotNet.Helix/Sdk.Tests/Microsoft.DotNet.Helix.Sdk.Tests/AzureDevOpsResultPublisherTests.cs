// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher.Model;
using Microsoft.DotNet.Helix.JobMonitor;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests;

public class AzureDevOpsResultPublisherTests
{
    private const int TestRunId = 123;

    [Fact]
    public void AttachmentModeDefaultsToFailed()
    {
        Assert.Equal(TestResultAttachmentMode.Failed, new JobMonitorOptions().TestResultAttachmentMode);
    }

    [Fact]
    public void JobMonitorUploadParallelismDefaultsToFortyEight()
    {
        Assert.Equal(48, new JobMonitorOptions().TestResultUploadParallelism);
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
        Assert.Equal(expectedAllPassed, TestResultProcessor.ComputeAllPassed(results));
    }

    [Fact]
    public void ComputeAllPassed_InconclusiveDataDrivenRollup_DoesNotFailTheWorkItem()
    {
        AggregatedResult[] results =
        [
            new(AggregationType.Single, "Test1", 1, "Passed"),
            new(AggregationType.DataDriven, "Test2", 1, "Inconclusive"),
        ];

        Assert.True(TestResultProcessor.ComputeAllPassed(results));
    }

    [Fact]
    public void ComputeAllPassed_AnyFailedResult_FailsTheWorkItem()
    {
        AggregatedResult[] results =
        [
            new(AggregationType.Single, "Test1", 1, "Passed"),
            new(AggregationType.DataDriven, "Test2", 1, "Failed"),
        ];

        Assert.False(TestResultProcessor.ComputeAllPassed(results));
    }

    [Fact]
    public async Task UploadTestResultsWithCountAsync_BatchesByTopLevelResultCount()
    {
        var transport = new RecordingResultTransport();
        var publisher = CreatePublisher(transport);
        AggregatedResult[] results =
        [
            CreateDataDrivenResult("First", 600),
            CreateDataDrivenResult("Second", 600),
        ];

        long uploadedCount = await PublishAsync(publisher, results);

        Assert.Equal(2, uploadedCount);
        Assert.Equal(new[] { 2 }, transport.RequestResultCounts);
        Assert.Equal([TestRunId], transport.TestRunIds);
    }

    [Fact]
    public async Task UploadTestResultsWithCountAsync_SplitsMoreThanOneThousandTopLevelResults()
    {
        var transport = new RecordingResultTransport();
        var publisher = CreatePublisher(transport);
        AggregatedResult[] results =
        [
            .. Enumerable.Range(0, 1001)
                .Select(i => new AggregatedResult(AggregationType.Single, $"Test{i}", 1, "Passed"))
        ];

        long uploadedCount = await PublishAsync(publisher, results);

        Assert.Equal(1001, uploadedCount);
        Assert.Equal(new[] { 1000, 1 }, transport.RequestResultCounts);
    }

    [Fact]
    public async Task UploadTestResultsWithCountAsync_SplitHierarchiesIncludeRootInNodeLimit()
    {
        var transport = new RecordingResultTransport();
        var publisher = CreatePublisher(transport);

        long uploadedCount = await PublishAsync(
            publisher,
            [CreateDataDrivenResult("Theory", 950)]);

        Assert.Equal(2, uploadedCount);
        Assert.Equal(new[] { 2 }, transport.RequestResultCounts);
        Assert.Equal(new[] { 950, 2 }, transport.RequestHierarchyNodeCounts.Single());
    }

    [Fact]
    public async Task UploadTestResultsWithCountAsync_RecursivelySplitsOversizedNestedHierarchies()
    {
        var transport = new RecordingResultTransport();
        var publisher = CreatePublisher(transport);
        var nested = CreateDataDrivenResult("Nested", 950);
        AggregatedResult[] results =
        [
            new(AggregationType.DataDriven, "Outer", 1, "Passed", [nested]),
        ];

        long uploadedCount = await PublishAsync(publisher, results);

        Assert.Equal(2, uploadedCount);
        Assert.Equal(new[] { 950, 4 }, transport.RequestHierarchyNodeCounts.Single());
    }

    [Fact]
    public async Task UploadTestResultsWithCountAsync_DoesNotMaterializeAllConvertedResults()
    {
        var transport = new BlockingResultTransport();
        var publisher = CreatePublisher(transport);
        var results = new TrackingResultList(2_000);

        Task<long> upload = PublishAsync(publisher, results);
        await transport.FirstRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.InRange(results.EnumeratedCount, 0, 1001);
        transport.ReleaseFirstRequest.SetResult();

        Assert.Equal(2_000, await upload);
        Assert.Equal(2, transport.RequestResultCounts.Count);
    }

    [Fact]
    public async Task UploadTestResultsWithCountAsync_UsesSemanticAttachmentTransport()
    {
        var transport = new RecordingResultTransport();
        var publisher = CreatePublisher(transport);
        var result = new AggregatedResult(
            AggregationType.Single,
            "Test",
            1,
            "Failed",
            attachments: [new TestResultAttachment("failure.txt", "details")]);

        Assert.Equal(1, await PublishAsync(publisher, [result]));

        ResultAttachment attachment = Assert.Single(transport.Attachments);
        Assert.Equal(1, attachment.TestResultId);
        Assert.Null(attachment.TestSubResultId);
        Assert.Equal("failure.txt", attachment.FileName);
        Assert.Equal("details", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(attachment.Stream)));
    }

    [Theory]
    [InlineData("{\"value\":[]}", 1)]
    [InlineData("{\"value\":[{\"id\":1}]}", 2)]
    [InlineData("{\"value\":[{\"id\":-1}]}", 1)]
    public async Task UploadTestResultsWithCountAsync_RejectsIncompleteResponses(string response, int resultCount)
    {
        var transport = new FixedResponseTransport(response);
        var publisher = CreatePublisher(transport);
        AggregatedResult[] results = [.. Enumerable.Range(0, resultCount)
            .Select(i => new AggregatedResult(AggregationType.Single, $"Test{i}", 1, "Passed"))];

        await Assert.ThrowsAsync<AzureDevOpsReportingError>(() => PublishAsync(publisher, results));
    }

    [Fact]
    public async Task UploadTestResultsWithCountAsync_UsesStableWorkItemNameAsTestStorage()
    {
        var transport = new RecordingResultTransport();
        var publisher = CreatePublisher(transport);
        var result = new AggregatedResult(AggregationType.Single, "Test", 1, "Passed");

        await PublishAsync(
            publisher,
            [result],
            "work-item",
            "job-a");
        await PublishAsync(
            publisher,
            [result],
            "work-item",
            "job-b");

        JsonElement firstResult = Assert.Single(transport.RequestBodies[0].EnumerateArray());
        JsonElement secondResult = Assert.Single(transport.RequestBodies[1].EnumerateArray());

        Assert.Equal("work-item", firstResult.GetProperty("AutomatedTestStorage").GetString());
        Assert.Equal("work-item", secondResult.GetProperty("AutomatedTestStorage").GetString());
        Assert.Contains("job-a", firstResult.GetProperty("Comment").GetString());
        Assert.Contains("job-b", secondResult.GetProperty("Comment").GetString());
    }

    [Fact]
    public async Task FullyQualifiedNames_DataDrivenRowsUseShortChildDisplayNames()
    {
        const string fullyQualifiedName =
            "Microsoft.DotNet.Cli.New.IntegrationTests.CommonTemplatesTests.FeaturesSupport";
        const string dataRowName = "FeaturesSupport(\"classlib\",True,\"netstandard2.0\")";
        var transport = new RecordingResultTransport();
        var publisher = CreatePublisher(transport, useFullyQualifiedTestName: true);
        var result = new AggregatedResult(
            AggregationType.DataDriven,
            fullyQualifiedName,
            1,
            "Passed",
            [new AggregatedResult(
                AggregationType.Single,
                dataRowName,
                1,
                "Passed",
                fullyQualifiedName: fullyQualifiedName)],
            fullyQualifiedName: fullyQualifiedName);

        await PublishAsync(publisher, [result]);

        JsonElement publishedTest = Assert.Single(Assert.Single(transport.RequestBodies).EnumerateArray());
        Assert.Equal(fullyQualifiedName, publishedTest.GetProperty("TestCaseTitle").GetString());
        JsonElement dataRow = Assert.Single(publishedTest.GetProperty("SubResults").EnumerateArray());
        Assert.Equal(dataRowName, dataRow.GetProperty("DisplayName").GetString());
    }

    [Fact]
    public async Task FullyQualifiedNames_DataDrivenRerunRowsOnlyShortenDirectChildren()
    {
        const string fullyQualifiedName = "Ns.MyTests.FeaturesSupport";
        const string dataRowName = "FeaturesSupport(\"classlib\")";
        var transport = new RecordingResultTransport();
        var publisher = CreatePublisher(transport, useFullyQualifiedTestName: true);
        var attempt = new AggregatedResult(
            AggregationType.Single,
            $"Attempt #1 - {dataRowName}",
            1,
            "Passed",
            attemptId: 1,
            fullyQualifiedName: fullyQualifiedName);
        var rerunRow = new AggregatedResult(
            AggregationType.Rerun,
            dataRowName,
            1,
            "Passed",
            [attempt],
            fullyQualifiedName: fullyQualifiedName);
        var result = new AggregatedResult(
            AggregationType.DataDriven,
            fullyQualifiedName,
            1,
            "Passed",
            [rerunRow],
            fullyQualifiedName: fullyQualifiedName);

        await PublishAsync(publisher, [result]);

        JsonElement publishedTest = Assert.Single(Assert.Single(transport.RequestBodies).EnumerateArray());
        JsonElement publishedRow = Assert.Single(publishedTest.GetProperty("SubResults").EnumerateArray());
        JsonElement publishedAttempt = Assert.Single(publishedRow.GetProperty("SubResults").EnumerateArray());
        Assert.Equal(dataRowName, publishedRow.GetProperty("DisplayName").GetString());
        Assert.Equal(
            $"{fullyQualifiedName} (Attempt #1 - {dataRowName})",
            publishedAttempt.GetProperty("DisplayName").GetString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InlineReporting_TrxPreservesDistinctClassIdentities(bool qualifiedNames, bool dataRows)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, "results.trx");
            string TestName(string className, int row)
                => (qualifiedNames ? className + "." : "") + "MyMethod" + (dataRows ? $"({row})" : "");
            await File.WriteAllTextAsync(file, $"""
                <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                  <Results>
                    <UnitTestResult testId="a" testName="{TestName("Ns.ClassA", 1)}" outcome="Passed" />
                    <UnitTestResult testId="b" testName="{TestName("Ns.ClassB", 1)}" outcome="Failed" />
                    {(dataRows ? $"""
                    <UnitTestResult testId="a" testName="{TestName("Ns.ClassA", 2)}" outcome="Passed" />
                    <UnitTestResult testId="b" testName="{TestName("Ns.ClassB", 2)}" outcome="Passed" />
                    """ : "")}
                  </Results>
                  <TestDefinitions>
                    <UnitTest id="a"><TestMethod className="Ns.ClassA" name="MyMethod" /></UnitTest>
                    <UnitTest id="b"><TestMethod className="Ns.ClassB" name="MyMethod" /></UnitTest>
                  </TestDefinitions>
                </TestRun>
                """);

            var processor = new TestResultProcessor(
                TestResultAttachmentMode.Failed, useFullyQualifiedTestName: false,
                NullLogger.Instance, new TestReportingMetrics(), failOnParseError: true, qualifyTrxTestNames: true);
            var workItem = new WorkItemTestResults("job", "work-item", [file]);
            PreparedTestResults prepared = await processor.PrepareAsync(workItem, CancellationToken.None);
            Assert.Equal(2, prepared.Results.Count);
            Assert.False(prepared.AllPassed);
            var transport = new RecordingResultTransport();

            Assert.Equal(2, await CreatePublisher(transport).PublishAsync(
                TestRunId, workItem, prepared, CancellationToken.None));

            JsonElement[] published = Assert.Single(transport.RequestBodies).EnumerateArray().ToArray();
            Assert.Equal(2, published.Length);
            for (int i = 0; i < published.Length; i++)
            {
                string name = $"Ns.Class{(i == 0 ? "A" : "B")}.MyMethod";
                Assert.Equal(name, published[i].GetProperty("TestCaseTitle").GetString());
                Assert.Equal(name, published[i].GetProperty("AutomatedTestName").GetString());
                Assert.Equal(i == 0 ? "Passed" : "Failed", published[i].GetProperty("Outcome").GetString());
                if (dataRows)
                {
                    Assert.Equal(
                        new[] { $"{name}(1)", $"{name}(2)" },
                        published[i].GetProperty("SubResults").EnumerateArray()
                            .Select(row => row.GetProperty("DisplayName").GetString()));
                }
                else
                {
                    Assert.Equal(JsonValueKind.Null, published[i].GetProperty("SubResults").ValueKind);
                }
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InlineReporting_TrxQualificationDoesNotChangeXunitCustomDisplayIdentities(bool qualifyTrxTestNames)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, "testResults.xml");
            await File.WriteAllTextAsync(file, """
                <assemblies><assembly><collection>
                  <test name="First custom display" type="Ns.Tests" method="MyMethod" result="Pass" />
                  <test name="Second custom display" type="Ns.Tests" method="MyMethod" result="Pass" />
                </collection></assembly></assemblies>
                """);
            var processor = new TestResultProcessor(
                TestResultAttachmentMode.Failed, useFullyQualifiedTestName: false,
                NullLogger.Instance, new TestReportingMetrics(), failOnParseError: true, qualifyTrxTestNames: qualifyTrxTestNames);
            var workItem = new WorkItemTestResults("job", "work-item", [file]);
            PreparedTestResults prepared = await processor.PrepareAsync(workItem, CancellationToken.None);
            Assert.Equal(2, prepared.Results.Count);
            var transport = new RecordingResultTransport();

            Assert.Equal(2, await CreatePublisher(transport).PublishAsync(
                TestRunId, workItem, prepared, CancellationToken.None));

            JsonElement[] published = Assert.Single(transport.RequestBodies).EnumerateArray().ToArray();
            Assert.Equal(new[] { "First custom display", "Second custom display" },
                published.Select(result => result.GetProperty("AutomatedTestName").GetString()));
            Assert.Equal(new[] { "First custom display", "Second custom display" },
                published.Select(result => result.GetProperty("TestCaseTitle").GetString()));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("failure", "")]
    [InlineData("failure", "  at Sample.Tests.Test()\n  at TestRunner.Run()")]
    [InlineData("error", "")]
    [InlineData("error", "  at Sample.Tests.Test()\n  at TestRunner.Run()")]
    public async Task InlineReporting_JUnitPublishesFailureDiagnostics(string elementName, string stackTrace)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, "junit-results.xml");
            await File.WriteAllTextAsync(file, $"""
                <testsuites><testsuite>
                  <testcase classname="Sample.Tests" name="Test" time="0.125">
                    <{elementName} type="AssertionError" message="assertion failed">{stackTrace}</{elementName}>
                    <system-out>test output</system-out>
                    <system-err>test error output</system-err>
                  </testcase>
                </testsuite></testsuites>
                """);
            var processor = new TestResultProcessor(
                TestResultAttachmentMode.Failed, useFullyQualifiedTestName: false,
                NullLogger.Instance, new TestReportingMetrics(), failOnParseError: true, qualifyTrxTestNames: true);
            var workItem = new WorkItemTestResults("job", "work-item", [file]);
            PreparedTestResults prepared = await processor.PrepareAsync(workItem, CancellationToken.None);
            Assert.False(prepared.AllPassed);
            var transport = new RecordingResultTransport();

            Assert.Equal(1, await CreatePublisher(transport).PublishAsync(
                TestRunId, workItem, prepared, CancellationToken.None));

            JsonElement published = Assert.Single(Assert.Single(transport.RequestBodies).EnumerateArray());
            Assert.Equal("Failed", published.GetProperty("Outcome").GetString());
            Assert.Equal("assertion failed", published.GetProperty("ErrorMessage").GetString());
            Assert.Equal(stackTrace, published.GetProperty("StackTrace").GetString());
            Assert.Equal(new[] { "test output", "test error output" },
                transport.Attachments.Select(attachment =>
                    System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(attachment.Stream))));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task InlineReporting_TrxPublishesSkipReasonWithoutAttachingStandardOutput()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, "results.trx");
            await File.WriteAllTextAsync(file, """
                <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                  <Results>
                    <UnitTestResult testId="a" testName="Test" outcome="NotExecuted">
                      <Output>
                        <StdOut>test is disabled</StdOut>
                        <ErrorInfo><Message>other message</Message></ErrorInfo>
                      </Output>
                    </UnitTestResult>
                  </Results>
                  <TestDefinitions>
                    <UnitTest id="a"><TestMethod className="Sample.Tests" name="Test" /></UnitTest>
                  </TestDefinitions>
                </TestRun>
                """);
            var processor = new TestResultProcessor(
                TestResultAttachmentMode.Failed, useFullyQualifiedTestName: false,
                NullLogger.Instance, new TestReportingMetrics(), failOnParseError: true, qualifyTrxTestNames: true);
            var workItem = new WorkItemTestResults("job", "work-item", [file]);
            PreparedTestResults prepared = await processor.PrepareAsync(workItem, CancellationToken.None);
            Assert.True(prepared.AllPassed);
            var transport = new RecordingResultTransport();

            Assert.Equal(1, await CreatePublisher(transport).PublishAsync(
                TestRunId, workItem, prepared, CancellationToken.None));

            JsonElement published = Assert.Single(Assert.Single(transport.RequestBodies).EnumerateArray());
            Assert.Equal("NotExecuted", published.GetProperty("Outcome").GetString());
            Assert.Equal("test is disabled", published.GetProperty("ErrorMessage").GetString());
            Assert.Empty(transport.Attachments);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task InlineReporting_XunitPublishesDecodedFailureMessage()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, "testResults.xml");
            await File.WriteAllTextAsync(file, """
                <assemblies><assembly><collection>
                  <test name="Sample.Tests.Test" type="Sample.Tests" method="Test" result="Fail">
                    <failure exception-type="AssertionError">
                      <message>  assertion failed\nExpected:\x20true\nActual:\tfalse  </message>
                      <stack-trace>at Sample.Tests.Test()</stack-trace>
                    </failure>
                  </test>
                </collection></assembly></assemblies>
                """);
            var processor = new TestResultProcessor(
                TestResultAttachmentMode.Failed, useFullyQualifiedTestName: false,
                NullLogger.Instance, new TestReportingMetrics(), failOnParseError: true, qualifyTrxTestNames: true);
            var workItem = new WorkItemTestResults("job", "work-item", [file]);
            PreparedTestResults prepared = await processor.PrepareAsync(workItem, CancellationToken.None);
            Assert.False(prepared.AllPassed);
            var transport = new RecordingResultTransport();

            Assert.Equal(1, await CreatePublisher(transport).PublishAsync(
                TestRunId, workItem, prepared, CancellationToken.None));

            JsonElement published = Assert.Single(Assert.Single(transport.RequestBodies).EnumerateArray());
            Assert.Equal("Failed", published.GetProperty("Outcome").GetString());
            Assert.Equal("  assertion failed\nExpected: true\nActual:\tfalse  ",
                published.GetProperty("ErrorMessage").GetString());
            Assert.Equal("at Sample.Tests.Test()", published.GetProperty("StackTrace").GetString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InlineReporting_TrxPublishesNestedRowsWithTheirOwnDiagnostics(bool parentHasOutput)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, "results.trx");
            await File.WriteAllTextAsync(file, $"""
                <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                  <Results>
                    <UnitTestResult testId="a" testName="DataTest" outcome="Failed">
                      <InnerResults>
                        <UnitTestResult testId="a" testName="DataTest(1)" outcome="Passed" />
                        <UnitTestResult testId="a" testName="DataTest(2)" outcome="Failed">
                          <InnerResults>
                            <UnitTestResult testId="a" testName="DataTest(2.1)" outcome="Failed">
                              <Output>
                                <StdOut>nested output</StdOut>
                                <ErrorInfo><Message>nested failure</Message><StackTrace>nested stack</StackTrace></ErrorInfo>
                              </Output>
                            </UnitTestResult>
                          </InnerResults>
                          <Output>
                            <StdOut>row output</StdOut>
                            <ErrorInfo><Message>row failure</Message><StackTrace>row stack</StackTrace></ErrorInfo>
                          </Output>
                        </UnitTestResult>
                        <UnitTestResult testId="a" testName="DataTest(3)" outcome="NotExecuted">
                          <Output><StdOut>row is disabled</StdOut></Output>
                        </UnitTestResult>
                      </InnerResults>
                      {(parentHasOutput ? """
                      <Output>
                        <StdOut>parent output</StdOut>
                        <ErrorInfo><Message>parent failure</Message><StackTrace>parent stack</StackTrace></ErrorInfo>
                      </Output>
                      """ : "")}
                    </UnitTestResult>
                  </Results>
                  <TestDefinitions>
                    <UnitTest id="a"><TestMethod className="Sample.Tests" name="DataTest" /></UnitTest>
                  </TestDefinitions>
                </TestRun>
                """);
            var reader = new LocalTestResultsReader(
                NullLogger.Instance, TestResultAttachmentMode.Failed, qualifyTrxTestNames: true);
            IReadOnlyList<TestResult> parsed = await reader.ReadResultFileAsync(file, failOnParseError: true);
            string[] names =
            [
                "Sample.Tests.DataTest(1)", "Sample.Tests.DataTest(2.1)", "Sample.Tests.DataTest(2)",
                "Sample.Tests.DataTest(3)", "Sample.Tests.DataTest",
            ];
            Assert.Equal(names, parsed.Select(result => result.Name));
            Assert.Equal(new[] { "Pass", "Fail", "Fail", "Skip", "Fail" }, parsed.Select(result => result.Result));
            Assert.Equal(new[] { null, "nested failure", "row failure", null, parentHasOutput ? "parent failure" : null },
                parsed.Select(result => result.FailureMessage));
            Assert.Equal(new[] { null, "nested stack", "row stack", null, parentHasOutput ? "parent stack" : null },
                parsed.Select(result => result.StackTrace));
            Assert.Equal("row is disabled", parsed[3].SkipReason);
            Assert.Null(parsed[4].SkipReason);
            Assert.Equal(new[] { 0, 1, 1, 0, parentHasOutput ? 1 : 0 }, parsed.Select(result => result.Attachments.Count));

            var processor = new TestResultProcessor(
                TestResultAttachmentMode.Failed, useFullyQualifiedTestName: false,
                NullLogger.Instance, new TestReportingMetrics(), failOnParseError: true, qualifyTrxTestNames: true);
            var workItem = new WorkItemTestResults("job", "work-item", [file]);
            PreparedTestResults prepared = await processor.PrepareAsync(workItem, CancellationToken.None);
            Assert.False(prepared.AllPassed);
            var transport = new RecordingResultTransport();

            Assert.Equal(1, await CreatePublisher(transport).PublishAsync(
                TestRunId, workItem, prepared, CancellationToken.None));

            JsonElement published = Assert.Single(Assert.Single(transport.RequestBodies).EnumerateArray());
            Assert.Equal("Failed", published.GetProperty("Outcome").GetString());
            JsonElement[] rows = published.GetProperty("SubResults").EnumerateArray().ToArray();
            Assert.Equal(names, rows.Select(row => row.GetProperty("DisplayName").GetString()));
            Assert.Equal(new[] { "Passed", "Failed", "Failed", "NotExecuted", "Failed" },
                rows.Select(row => row.GetProperty("Outcome").GetString()));
            Assert.Equal(new[] { null, "nested failure", "row failure", "row is disabled", parentHasOutput ? "parent failure" : null },
                rows.Select(row => row.GetProperty("ErrorMessage").GetString()));
            Assert.Equal(new[] { null, "nested stack", "row stack", null, parentHasOutput ? "parent stack" : null },
                rows.Select(row => row.GetProperty("StackTrace").GetString()));
            Assert.Equal(parentHasOutput ? 3 : 2, transport.Attachments.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static AzureDevOpsResultPublisher CreatePublisher(
        IAzureDevOpsResultTransport transport,
        bool useFullyQualifiedTestName = false)
        => new(
            NullLogger.Instance,
            useFullyQualifiedTestName,
            transport);

    private static Task<long> PublishAsync(
        AzureDevOpsResultPublisher publisher,
        IReadOnlyList<AggregatedResult> results,
        string workItemName = "work-item",
        string jobId = "job")
        => publisher.PublishAsync(
            TestRunId,
            new WorkItemTestResults(jobId, workItemName, []),
            new PreparedTestResults(results, AllPassed: true),
            CancellationToken.None);

    private static AggregatedResult CreateDataDrivenResult(string name, int subResultCount)
        => new(
            AggregationType.DataDriven,
            name,
            subResultCount,
            "Passed",
            [
                .. Enumerable.Range(0, subResultCount)
                    .Select(i => new AggregatedResult(AggregationType.Single, $"{name}_{i}", 1, "Passed"))
            ]);

    private class RecordingResultTransport : IAzureDevOpsResultTransport
    {
        public List<int> TestRunIds { get; } = [];
        public List<int> RequestResultCounts { get; } = [];
        public List<int[]> RequestHierarchyNodeCounts { get; } = [];
        public List<JsonElement> RequestBodies { get; } = [];
        public List<ResultAttachment> Attachments { get; } = [];

        public virtual Task<string> PublishResultsAsync(
            int testRunId,
            object results,
            CancellationToken cancellationToken)
        {
            TestRunIds.Add(testRunId);
            using JsonDocument requestBody = JsonDocument.Parse(JsonSerializer.Serialize(results));
            RequestBodies.Add(requestBody.RootElement.Clone());
            int resultCount = requestBody.RootElement.GetArrayLength();
            RequestResultCounts.Add(resultCount);
            RequestHierarchyNodeCounts.Add(
                [.. requestBody.RootElement.EnumerateArray().Select(CountHierarchyNodes)]);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                value = requestBody.RootElement.EnumerateArray().Select((result, index) => ResultReference(result, index + 1))
            }));
        }

        private static object ResultReference(JsonElement result, int id)
            => new
            {
                id,
                subResults = result.TryGetProperty("SubResults", out JsonElement children) && children.ValueKind == JsonValueKind.Array
                    ? children.EnumerateArray().Select((child, index) => ResultReference(child, index + 1)).ToArray()
                    : null,
            };

        public Task UploadAttachmentAsync(
            int testRunId,
            long testResultId,
            long? testSubResultId,
            string fileName,
            string stream,
            CancellationToken cancellationToken)
        {
            TestRunIds.Add(testRunId);
            Attachments.Add(new(testResultId, testSubResultId, fileName, stream));
            return Task.CompletedTask;
        }

        private static int CountHierarchyNodes(JsonElement result)
        {
            if (!result.TryGetProperty("SubResults", out JsonElement subResults) &&
                !result.TryGetProperty("subResults", out subResults))
            {
                return 1;
            }

            return subResults.ValueKind == JsonValueKind.Array
                ? 1 + subResults.EnumerateArray().Sum(CountHierarchyNodes)
                : 1;
        }
    }

    private sealed class FixedResponseTransport(string response) : RecordingResultTransport
    {
        public override Task<string> PublishResultsAsync(int testRunId, object results, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private sealed class BlockingResultTransport : RecordingResultTransport
    {
        public TaskCompletionSource FirstRequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstRequest { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requestCount;

        public override async Task<string> PublishResultsAsync(
            int testRunId,
            object results,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _requestCount) == 1)
            {
                FirstRequestStarted.SetResult();
                await ReleaseFirstRequest.Task.WaitAsync(cancellationToken);
            }

            return await base.PublishResultsAsync(testRunId, results, cancellationToken);
        }
    }

    private sealed class TrackingResultList(int count) : IReadOnlyList<AggregatedResult>
    {
        public int Count => count;
        public int EnumeratedCount { get; private set; }

        public AggregatedResult this[int index] => CreateResult(index);

        public IEnumerator<AggregatedResult> GetEnumerator()
        {
            for (int i = 0; i < count; i++)
            {
                EnumeratedCount++;
                yield return CreateResult(i);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();

        private static AggregatedResult CreateResult(int index)
            => new(AggregationType.Single, $"Test{index}", 1, "Passed");
    }

    private sealed record ResultAttachment(
        long TestResultId,
        long? TestSubResultId,
        string FileName,
        string Stream);
}
