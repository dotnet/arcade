// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// TODO: Not yet supported for xunit.v3
#if !USES_XUNIT_3
using System;
using System.Linq;
#if !USES_XUNIT_3
using Xunit.Abstractions;
#endif
using Xunit.Sdk;

namespace Microsoft.DotNet.XUnitExtensions
{
    public class SkipTestException : Exception
    {
        public SkipTestException(string reason)
            : base(reason) { }
    }

    /// <summary>Implements message bus to communicate tests skipped via SkipTestException.</summary>
    public class SkippedTestMessageBus : IMessageBus
    {
        readonly IMessageBus innerBus;

        public SkippedTestMessageBus(IMessageBus innerBus)
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
}
#endif
