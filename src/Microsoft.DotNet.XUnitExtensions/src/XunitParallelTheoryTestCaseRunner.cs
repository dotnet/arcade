// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.DotNet.XUnitExtensions
{
    internal sealed class XunitParallelTheoryTestCaseRunner : XunitTheoryTestCaseRunner
    {
        readonly ExceptionAggregator _cleanupAggregator = new();

        public XunitParallelTheoryTestCaseRunner(IXunitTestCase testCase, string displayName, string skipReason, object[] constructorArguments, IMessageSink diagnosticMessageSink, IMessageBus messageBus, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
            : base(testCase, displayName, skipReason, constructorArguments, diagnosticMessageSink, messageBus, aggregator, cancellationTokenSource)
        {
            // We want to use the same data discovery functionality of the base type, but the results are not exposed to subclasses.
            // Use reflection to get the data. Not the best, but it works.
            DataDiscoveryException = (Func<Exception>)typeof(XunitTheoryTestCaseRunner)
                .GetProperty("dataDiscoveryException", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetGetMethod(true)
                .CreateDelegate(typeof(Func<Exception>), this);
            TestRunners = (Func<List<XunitTestRunner>>)typeof(XunitTheoryTestCaseRunner)
                .GetProperty("testRunners", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetGetMethod(true)
                .CreateDelegate(typeof(Func<List<XunitTestRunner>>), this);
            ToDispose = (Func<List<IDisposable>>)typeof(XunitTheoryTestCaseRunner)
                .GetProperty("toDispose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetGetMethod(true)
                .CreateDelegate(typeof(Func<List<IDisposable>>), this);
        }

        private Func<Exception> DataDiscoveryException { get; }

        private Func<List<XunitTestRunner>> TestRunners { get; }

        private Func<List<IDisposable>> ToDispose { get; }

        /// <inheritdoc/>
        protected override async Task<RunSummary> RunTestAsync()
        {
            if (DataDiscoveryException() != null)
                return await base.RunTestAsync();

            var runningTests = new List<Task<RunSummary>>(TestRunners().Count);
            foreach (var testRunner in TestRunners())
                runningTests.Add(testRunner.RunAsync());

            var results = await Task.WhenAll(runningTests);
            var runSummary = new RunSummary();
            foreach (var result in results)
            {
                runSummary.Aggregate(result);
            }
            // Run the cleanup here so we can include cleanup time in the run summary,
            // but save any exceptions so we can surface them during the cleanup phase,
            // so they get properly reported as test case cleanup failures.
            var timer = new ExecutionTimer();
            foreach (var disposable in ToDispose())
                timer.Aggregate(() => _cleanupAggregator.Run(disposable.Dispose));

            runSummary.Time += timer.Total;
            return runSummary;
        }

        /// <inheritdoc/>
        protected override Task BeforeTestCaseFinishedAsync()
        {
            Aggregator.Aggregate(_cleanupAggregator);
            return base.BeforeTestCaseFinishedAsync();
        }
    }
}
