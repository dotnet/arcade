// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            Type declaringType = testMethodInfo.DeclaringType;
            string[] symbols = conditionMemberName.Split('.');

            if (symbols.Length == 2)
            {
                conditionMemberName = symbols[1];
                ITypeInfo type = testMethod.TestClass.Class.Assembly.GetTypes(false).Where(t => t.Name.Contains(symbols[0])).FirstOrDefault();
                if (type != null)
                {
                    declaringType = type.ToRuntimeType();
                }
            }
            
            MethodInfo conditionMethodInfo;
            if (conditionMemberName == null ||
                (conditionMethodInfo = LookupConditionalMethod(declaringType, conditionMemberName)) == null)
            {
                return new[] {
                    new ExecutionErrorTestCase(
                        _diagnosticMessageSink,
                        discoveryOptions.MethodDisplayOrDefault(),
                        testMethod,
                        GetFailedLookupString(conditionMemberName))
                };
            }

            IEnumerable<IXunitTestCase> testCases = base.Discover(discoveryOptions, testMethod, factAttribute);
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

        internal static string GetFailedLookupString(string name)
        {
            return
                "An appropriate member \"" + name + "\" could not be found. " +
                "The conditional method needs to be a static method or property on this or any ancestor type, " +
                "of any visibility, accepting zero arguments, and having a return type of Boolean.";
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
