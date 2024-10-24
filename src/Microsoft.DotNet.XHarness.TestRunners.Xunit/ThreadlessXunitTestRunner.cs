// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using Xunit;
using Xunit.Abstractions;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

internal class ThreadlessXunitTestRunner : XunitTestRunnerBase
{
    public ThreadlessXunitTestRunner(LogWriter logger, bool oneLineResults = false) : base(logger)
    {
        _oneLineResults = oneLineResults;
        ShowFailureInfos = false;
    }

    protected override string ResultsFileName { get => string.Empty; set => throw new InvalidOperationException("This runner outputs its results to stdout."); }

    private readonly XElement _assembliesElement = new XElement("assemblies");
    private readonly bool _oneLineResults;

    public override async Task Run(IEnumerable<TestAssemblyInfo> testAssemblies)
    {
        OnInfo("Using threadless Xunit runner");

        var configuration = new TestAssemblyConfiguration() { ShadowCopy = false, ParallelizeAssembly = false, ParallelizeTestCollections = false, MaxParallelThreads = 1, PreEnumerateTheories = false };
        var discoveryOptions = TestFrameworkOptions.ForDiscovery(configuration);
        var discoverySink = new TestDiscoverySink();
        var diagnosticSink = new ConsoleDiagnosticMessageSink(Logger);
        var testOptions = TestFrameworkOptions.ForExecution(configuration);
        var testSink = new TestMessageSink();

        var totalSummary = new ExecutionSummary();
        foreach (var testAsmInfo in testAssemblies)
        {
            string assemblyFileName = testAsmInfo.FullPath;
            var controller = new Xunit2(AppDomainSupport.Denied, new NullSourceInformationProvider(), assemblyFileName, configFileName: null, shadowCopy: false, shadowCopyFolder: null, diagnosticMessageSink: diagnosticSink, verifyTestAssemblyExists: false);

            discoveryOptions.SetSynchronousMessageReporting(true);
            testOptions.SetSynchronousMessageReporting(true);

            OnInfo($"Discovering: {assemblyFileName} (method display = {discoveryOptions.GetMethodDisplayOrDefault()}, method display options = {discoveryOptions.GetMethodDisplayOptionsOrDefault()})");
            var assemblyInfo = new global::Xunit.Sdk.ReflectionAssemblyInfo(testAsmInfo.Assembly);
            var discoverer = new ThreadlessXunitDiscoverer(assemblyInfo, new NullSourceInformationProvider(), discoverySink);

            discoverer.FindWithoutThreads(includeSourceInformation: false, discoverySink, discoveryOptions);
            var testCasesToRun = discoverySink.TestCases.Where(t => !_filters.IsExcluded(t)).ToList();
            OnInfo($"Discovered:  {assemblyFileName} (found {testCasesToRun.Count} of {discoverySink.TestCases.Count} test cases)");

            var summaryTaskSource = new TaskCompletionSource<ExecutionSummary>();
            var resultsXmlAssembly = new XElement("assembly");
#pragma warning disable CS0618 // Delegating*Sink types are marked obsolete, but we can't move to ExecutionSink yet: https://github.com/dotnet/arcade/issues/14375
            var resultsSink = new DelegatingXmlCreationSink(new DelegatingExecutionSummarySink(testSink), resultsXmlAssembly);
#pragma warning restore
            var completionSink = new CompletionCallbackExecutionSink(resultsSink, summary => summaryTaskSource.SetResult(summary));

            if (EnvironmentVariables.IsLogTestStart())
            {
                testSink.Execution.TestStartingEvent += args => { OnInfo($"[STRT] {args.Message.Test.DisplayName}"); };
            }
            testSink.Execution.TestPassedEvent += args =>
            {
                OnDebug($"[PASS] {args.Message.Test.DisplayName}");
                PassedTests++;
            };
            testSink.Execution.TestSkippedEvent += args =>
            {
                OnDebug($"[SKIP] {args.Message.Test.DisplayName}");
                SkippedTests++;
            };
            testSink.Execution.TestFailedEvent += args =>
            {
                OnError($"[FAIL] {args.Message.Test.DisplayName}{Environment.NewLine}{ExceptionUtility.CombineMessages(args.Message)}{Environment.NewLine}{ExceptionUtility.CombineStackTraces(args.Message)}");
                FailedTests++;
            };
            testSink.Execution.TestFinishedEvent += args => ExecutedTests++;

            testSink.Execution.TestAssemblyStartingEvent += args => { Console.WriteLine($"Starting:    {assemblyFileName}"); };
            testSink.Execution.TestAssemblyFinishedEvent += args => { Console.WriteLine($"Finished:    {assemblyFileName}"); };

            controller.RunTests(testCasesToRun, completionSink, testOptions);

            totalSummary = Combine(totalSummary, await summaryTaskSource.Task);

            _assembliesElement.Add(resultsXmlAssembly);
        }
        TotalTests = totalSummary.Total;
    }

    private ExecutionSummary Combine(ExecutionSummary aggregateSummary, ExecutionSummary assemblySummary)
    {
        return new ExecutionSummary
        {
            Total = aggregateSummary.Total + assemblySummary.Total,
            Failed = aggregateSummary.Failed + assemblySummary.Failed,
            Skipped = aggregateSummary.Skipped + assemblySummary.Skipped,
            Errors = aggregateSummary.Errors + assemblySummary.Errors,
            Time = aggregateSummary.Time + assemblySummary.Time
        };
    }

    public override string WriteResultsToFile(XmlResultJargon xmlResultJargon)
    {
        Debug.Assert(xmlResultJargon == XmlResultJargon.xUnit);
        WriteResultsToFile(Console.Out, xmlResultJargon);
        return "";
    }

    public override void WriteResultsToFile(TextWriter writer, XmlResultJargon jargon)
    {
        if (_oneLineResults)
        {
            WasmXmlResultWriter.WriteOnSingleLine(_assembliesElement);
        }
        else
        {
            writer.WriteLine($"STARTRESULTXML");
            _assembliesElement.Save(writer);
            writer.WriteLine();
            writer.WriteLine($"ENDRESULTXML");
        }
    }
}

internal class ThreadlessXunitDiscoverer : global::Xunit.Sdk.XunitTestFrameworkDiscoverer
{
    public ThreadlessXunitDiscoverer(IAssemblyInfo assemblyInfo, ISourceInformationProvider sourceProvider, IMessageSink diagnosticMessageSink)
        : base(assemblyInfo, sourceProvider, diagnosticMessageSink)
    {
    }

    public void FindWithoutThreads(bool includeSourceInformation, IMessageSink discoveryMessageSink, ITestFrameworkDiscoveryOptions discoveryOptions)
    {
#pragma warning disable CS0618 // SynchronousMessageBus ctor is marked obsolete
        using (var messageBus = new global::Xunit.Sdk.SynchronousMessageBus(discoveryMessageSink))
#pragma warning restore
        {
            foreach (var type in AssemblyInfo.GetTypes(includePrivateTypes: false).Where(IsValidTestClass))
            {
                var testClass = CreateTestClass(type);
                if (!FindTestsForType(testClass, includeSourceInformation, messageBus, discoveryOptions))
                {
                    break;
                }
            }

            messageBus.QueueMessage(new global::Xunit.Sdk.DiscoveryCompleteMessage());
        }
    }
}

internal class ConsoleDiagnosticMessageSink(LogWriter logger) : global::Xunit.Sdk.LongLivedMarshalByRefObject, IMessageSink
{
    public bool OnMessage(IMessageSinkMessage message)
    {
        if (message is IDiagnosticMessage diagnosticMessage)
        {
            logger.OnDebug(diagnosticMessage.Message);
        }

        return true;
    }
}
