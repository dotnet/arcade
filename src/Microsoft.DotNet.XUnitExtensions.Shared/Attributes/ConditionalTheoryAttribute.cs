// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// TODO: Not yet supported for xunit.v3
#if !USES_XUNIT_3
using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.XUnitExtensions;
using Xunit.Sdk;

namespace Xunit
{
#if USES_XUNIT_3
    [XunitTestCaseDiscoverer(typeof(ConditionalTheoryDiscoverer))]
#else
    [XunitTestCaseDiscoverer("Microsoft.DotNet.XUnitExtensions.ConditionalTheoryDiscoverer", "Microsoft.DotNet.XUnitExtensions")]
#endif
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ConditionalTheoryAttribute : TheoryAttribute
    {
        [DynamicallyAccessedMembers(StaticReflectionConstants.ConditionalMemberKinds)]
        public Type     CalleeType { get; private set; }
        public string[] ConditionMemberNames { get; private set; }

        public ConditionalTheoryAttribute(
            [DynamicallyAccessedMembers(StaticReflectionConstants.ConditionalMemberKinds)]
            Type calleeType,
            params string[] conditionMemberNames)
        {
            CalleeType = calleeType;
            ConditionMemberNames = conditionMemberNames;
        }

        public ConditionalTheoryAttribute(params string[] conditionMemberNames)
        {
            ConditionMemberNames = conditionMemberNames;
        }
    }
}
#endif
