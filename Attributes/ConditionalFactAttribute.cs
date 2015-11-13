// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit.Sdk;

namespace Xunit
{
    [XunitTestCaseDiscoverer("Xunit.NetCore.Extensions.ConditionalFactDiscoverer", "Xunit.NetCore.Extensions")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ConditionalFactAttribute : FactAttribute
    {
        public string ConditionMemberName { get; private set; }

        public ConditionalFactAttribute(string conditionMemberName)
        {
            ConditionMemberName = conditionMemberName;
        }
    }
}
