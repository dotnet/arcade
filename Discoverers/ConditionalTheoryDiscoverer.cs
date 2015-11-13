// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    public class ConditionalTheoryDiscoverer : TheoryDiscoverer
    {
        private readonly IMessageSink _diagnosticMessageSink;

        public ConditionalTheoryDiscoverer(IMessageSink diagnosticMessageSink) : base(diagnosticMessageSink)
        {
            _diagnosticMessageSink = diagnosticMessageSink;
        }

        public override IEnumerable<IXunitTestCase> Discover(
            ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo theoryAttribute)
        {
            MethodInfo testMethodInfo = testMethod.Method.ToRuntimeMethod();

            string conditionMemberName = theoryAttribute.GetConstructorArguments().FirstOrDefault() as string;
            MethodInfo conditionMethodInfo;
            if (conditionMemberName == null ||
                (conditionMethodInfo = ConditionalFactDiscoverer.LookupConditionalMethod(testMethodInfo.DeclaringType, conditionMemberName)) == null)
            {
                return new[] {
                    new ExecutionErrorTestCase(
                        _diagnosticMessageSink,
                        discoveryOptions.MethodDisplayOrDefault(),
                        testMethod,
                        ConditionalFactDiscoverer.GetFailedLookupString(conditionMemberName))
                };
            }

            IEnumerable<IXunitTestCase> testCases = base.Discover(discoveryOptions, testMethod, theoryAttribute);
            if ((bool)conditionMethodInfo.Invoke(null, null))
            {
                return testCases;
            }
            else
            {
                string skippedReason = "\"" + conditionMemberName + "\" returned false.";
                return testCases.Select(tc => new SkippedTestCase(tc, skippedReason));
            }
        }
    }
}
