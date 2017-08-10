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
    // Internal helper class for code common to conditional test discovery through
    // [ConditionalFact] and [ConditionalTheory]
    internal class ConditionalTestDiscoverer
    {
        // This helper method evaluates the given condition member names for a given set of test cases.
        // If any condition member evaluates to 'false', the test cases are marked to be skipped.
        // The skip reason is the collection of all the condition members that evalated to 'false'.
        internal static IEnumerable<IXunitTestCase> Discover(
                                                        ITestFrameworkDiscoveryOptions discoveryOptions,
                                                        IMessageSink diagnosticMessageSink,
                                                        ITestMethod testMethod,
                                                        IEnumerable<IXunitTestCase> testCases,
                                                        IEnumerable<string> conditionMemberNames)
        {
            // A null or empty list of conditionMemberNames is treated as "no conditions".
            // and the test cases will not be skipped.
            // Example: [ConditionalFact()] or [ConditionalFact((string[]) null)]
            int conditionCount = conditionMemberNames == null ? 0 : conditionMemberNames.Count();
            if (conditionCount == 0)
            {
                return testCases;
            }

            MethodInfo testMethodInfo = testMethod.Method.ToRuntimeMethod();
            Type testMethodDeclaringType = testMethodInfo.DeclaringType;
            List<string> falseConditions = new List<string>(conditionCount);

            foreach (string entry in conditionMemberNames)
            {
                string conditionMemberName = entry;

                // Null condition member names are silently tolerated
                if (string.IsNullOrWhiteSpace(conditionMemberName))
                {
                    continue;
                }

                Type declaringType = testMethodDeclaringType;

                // We have qualified type name with the assembly name, something like
                // [ConditionalFact("System.PlatformDetection, CoreFx.Private.TestUtilities!" + nameof(PlatformDetection.IsNonZeroLowerBoundArraySupported))]
                // We don't use '.' as separator in such case and use '!' because the qualified type name can have '.' to include the type namespace.
                if (conditionMemberName.IndexOf('!') > 0)
                {
                    // get the method name
                    string[] symbols = conditionMemberName.Split('!');
                    if (symbols.Length == 2)
                    {
                        conditionMemberName = symbols[1];
                        Type requestedType = Type.GetType(symbols[0]);
                        if (requestedType != null)
                        {
                            declaringType = requestedType;
                        }
                    }
                }
                else
                {
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
                }

                MethodInfo conditionMethodInfo;
                if ((conditionMethodInfo = LookupConditionalMethod(declaringType, conditionMemberName)) == null)
                {
                    return new[] 
                    {
                        new ExecutionErrorTestCase(
                            diagnosticMessageSink,
                            discoveryOptions.MethodDisplayOrDefault(),
                            testMethod,
                            GetFailedLookupString(conditionMemberName))
                    };
                }

                // In the case of multiple conditions, collect the results of all
                // of them to produce a summary skip reason.
                try
                {
                    if (!(bool)conditionMethodInfo.Invoke(null, null))
                    {
                        falseConditions.Add(conditionMemberName);
                    }
                }
                catch (Exception exc)
                {
                    falseConditions.Add($"{conditionMemberName} ({exc.GetType().Name})");
                }
            }

            // Compose a summary of all conditions that returned false.
            if (falseConditions.Count > 0)
            {
                string skippedReason = string.Format("Condition(s) not met: \"{0}\"", string.Join("\", \"", falseConditions));
                return testCases.Select(tc => new SkippedTestCase(tc, skippedReason));
            }

            // No conditions returned false (including the absence of any conditions).
            return testCases;
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
