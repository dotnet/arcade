// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// TODO: Not yet supported for xunit.v3
#if !USES_XUNIT_3
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

#if !USES_XUNIT_3
using Xunit.Abstractions;
#endif
using Xunit.Sdk;

namespace Microsoft.DotNet.XUnitExtensions
{
    public class ConditionalFactDiscoverer : FactDiscoverer
    {
#if !USES_XUNIT_3
        public ConditionalFactDiscoverer(IMessageSink diagnosticMessageSink) : base(diagnosticMessageSink) { }
#endif

#if USES_XUNIT_3
        protected override IXunitTestCase CreateTestCase(ITestFrameworkDiscoveryOptions discoveryOptions, IXunitTestMethod testMethod, IFactAttribute factAttribute)
#else
        protected override IXunitTestCase CreateTestCase(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
#endif
        {
#if USES_XUNIT_3
            var conditionalFactAttribute = (ConditionalFactAttribute)factAttribute;
            if (ConditionalTestDiscoverer.TryEvaluateSkipConditions(discoveryOptions, testMethod, [conditionalFactAttribute.CalleeType, conditionalFactAttribute.ConditionMemberNames], out string skipReason, out ExecutionErrorTestCase errorTestCase))
#else
            if (ConditionalTestDiscoverer.TryEvaluateSkipConditions(discoveryOptions, DiagnosticMessageSink, testMethod, factAttribute.GetConstructorArguments().ToArray(), out string skipReason, out ExecutionErrorTestCase errorTestCase))
#endif
            {
                return skipReason != null
                    ? (IXunitTestCase) new SkippedTestCase(skipReason, DiagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod)
                    : new SkippedFactTestCase(DiagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod); // Test case skippable at runtime.
            }

            return errorTestCase;
        }
    }
}
#endif
