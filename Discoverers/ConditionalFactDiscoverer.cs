// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    public class ConditionalFactDiscoverer : FactDiscoverer
    {
        private readonly IMessageSink _diagnosticMessageSink;

        public ConditionalFactDiscoverer(IMessageSink diagnosticMessageSink) : base(diagnosticMessageSink)
        {
            _diagnosticMessageSink = diagnosticMessageSink;
        }

        public override IEnumerable<IXunitTestCase> Discover(
            ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
        {
            MethodInfo testMethodInfo = testMethod.Method.ToRuntimeMethod();

            string conditionMemberName = factAttribute.GetConstructorArguments().FirstOrDefault() as string;
            MethodInfo conditionMethodInfo;
            if (conditionMemberName == null ||
                (conditionMethodInfo = LookupConditionalMethod(testMethodInfo.DeclaringType, conditionMemberName)) == null)
            {
                return new[] {
                    new ExecutionErrorTestCase(
                        _diagnosticMessageSink,
                        discoveryOptions.MethodDisplayOrDefault(),
                        testMethod,
                        "An appropriate member \"" + conditionMemberName + "\" could not be found.")
                };
            }

            object result = conditionMethodInfo.Invoke(null, null);
            return (bool)result ?
                base.Discover(discoveryOptions, testMethod, factAttribute) :
                Array.Empty<IXunitTestCase>();
        }

        internal static MethodInfo LookupConditionalMethod(Type t, string name)
        {
            if (t == null || name == null)
                return null;

            TypeInfo ti = t.GetTypeInfo();

            MethodInfo mi = ti.GetDeclaredMethod(name);
            if (mi != null && mi.IsStatic && mi.GetParameters().Length == 0 && mi.ReturnType == typeof(bool))
                return mi;

            PropertyInfo pi = ti.GetDeclaredProperty(name);
            if (pi != null && pi.PropertyType == typeof(bool) && pi.GetMethod != null && pi.GetMethod.IsStatic && pi.GetMethod.GetParameters().Length == 0)
                return pi.GetMethod;

            return LookupConditionalMethod(ti.BaseType, name);
        }
    }
}
