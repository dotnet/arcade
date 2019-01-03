// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.DotNet.XUnitExtensions
{

    public class SkipTestException : Exception
    {
        public SkipTestException(string reason)
            : base(reason) { }
    }

    /// <summary>Implements message buss to communicate tests skipped via SkipTestException.</summary>
    public class ConditionalTestMessageBus : IMessageBus
    {
        readonly IMessageBus innerBus;

        public ConditionalTestMessageBus(IMessageBus innerBus)
        {
            this.innerBus = innerBus;
        }

        public int SkippedTestCount { get; private set; }

        public void Dispose() { }

        public bool QueueMessage(IMessageSinkMessage message)
        {
            var testFailed = message as ITestFailed;

            if (testFailed != null)
            {
                var exceptionType = testFailed.ExceptionTypes.FirstOrDefault();
                if (exceptionType == typeof(SkipTestException).FullName)
                {
                    SkippedTestCount++;
                    return innerBus.QueueMessage(new TestSkipped(testFailed.Test, testFailed.Messages.FirstOrDefault()));
                }
            }

            // Nothing we care about, send it on its way
            return innerBus.QueueMessage(message);
        }
    }

    /// <summary>Wraps RunAsync for ConditionalTheory.</summary>
    public class SkippedTheoryTestCase : XunitTheoryTestCase
    {
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public SkippedTheoryTestCase() { }

        public SkippedTheoryTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay defaultMethodDisplay, TestMethodDisplayOptions defaultMethodDisplayOptions, ITestMethod testMethod)
            : base(diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod) { }

        public override async Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink,
                                                        IMessageBus messageBus,
                                                        object[] constructorArguments,
                                                        ExceptionAggregator aggregator,
                                                        CancellationTokenSource cancellationTokenSource)
        {
            // Duplicated code from SkippableFactTestCase. I'm sure we could find a way to de-dup with some thought.
            var skipMessageBus = new ConditionalTestMessageBus(messageBus);
            var result = await base.RunAsync(diagnosticMessageSink, skipMessageBus, constructorArguments, aggregator, cancellationTokenSource);
            if (skipMessageBus.SkippedTestCount > 0)
            {
                result.Failed -= skipMessageBus.SkippedTestCount;
                result.Skipped += skipMessageBus.SkippedTestCount;
            }

            return result;
        }
    }

    /// <summary>Wraps RunAsync for ConditionalFact.</summary>
    public class SkippedFactTestCase : XunitTestCase
    {
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public SkippedFactTestCase() { }

        public SkippedFactTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay defaultMethodDisplay, TestMethodDisplayOptions defaultMethodDisplayOptions, ITestMethod testMethod, object[] testMethodArguments = null)
            : base(diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod, testMethodArguments) { }

        public override async Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink,
                                                        IMessageBus messageBus,
                                                        object[] constructorArguments,
                                                        ExceptionAggregator aggregator,
                                                        CancellationTokenSource cancellationTokenSource)
        {
            // Duplicated code from SkippableFactTestCase. I'm sure we could find a way to de-dup with some thought.
            var skipMessageBus = new ConditionalTestMessageBus(messageBus);
            var result = await base.RunAsync(diagnosticMessageSink, skipMessageBus, constructorArguments, aggregator, cancellationTokenSource);
            if (skipMessageBus.SkippedTestCount > 0)
            {
                result.Failed -= skipMessageBus.SkippedTestCount;
                result.Skipped += skipMessageBus.SkippedTestCount;
            }

            return result;
        }
    }

    /// <summary>Wraps another test case that should be skipped.</summary>
    internal sealed class SkippedTestCase : LongLivedMarshalByRefObject, IXunitTestCase
    {
        private readonly IXunitTestCase _testCase;
        private readonly string _skippedReason;

        public SkippedTestCase()
        {
        }

        internal SkippedTestCase(IXunitTestCase testCase, string skippedReason)
        {
            _testCase = testCase;
            _skippedReason = skippedReason;
        }

        public string DisplayName { get { return _testCase.DisplayName; } }

        public IMethodInfo Method { get { return _testCase.Method; } }

        public string SkipReason { get { return _skippedReason; } }

        public ISourceInformation SourceInformation { get { return _testCase.SourceInformation; } set { _testCase.SourceInformation = value; } }

        public ITestMethod TestMethod { get { return _testCase.TestMethod; } }

        public object[] TestMethodArguments { get { return _testCase.TestMethodArguments; } }

        public Dictionary<string, List<string>> Traits { get { return _testCase.Traits; } }

        public string UniqueID { get { return _testCase.UniqueID; } }

        public Exception InitializationException { get { return null; } }

        public int Timeout { get { return 0; } }

        public void Deserialize(IXunitSerializationInfo info) { _testCase.Deserialize(info); }

        public Task<RunSummary> RunAsync(
            IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments,
            ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        {
            return new XunitTestCaseRunner(this, DisplayName, _skippedReason, constructorArguments, TestMethodArguments, messageBus, aggregator, cancellationTokenSource).RunAsync();
        }

        public void Serialize(IXunitSerializationInfo info) { _testCase.Serialize(info); }
    }
}
