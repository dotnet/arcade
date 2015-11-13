// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit.Sdk;

namespace Xunit
{
    [XunitTestCaseDiscoverer("Xunit.NetCore.Extensions.ConditionalTheoryDiscoverer", "Xunit.NetCore.Extensions")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ConditionalTheoryAttribute : TheoryAttribute
    {
        public string ConditionMemberName { get; private set; }

        public ConditionalTheoryAttribute(string conditionMemberName)
        {
            ConditionMemberName = conditionMemberName;
        }
    }
}
