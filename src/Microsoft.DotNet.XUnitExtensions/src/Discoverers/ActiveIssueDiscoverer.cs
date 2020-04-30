// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.DotNet.XUnitExtensions
{
    /// <summary>
    /// This class discovers all of the tests, test classes and test assemblies that have
    /// applied the ActiveIssue attribute
    /// </summary>
    public class ActiveIssueDiscoverer : ITraitDiscoverer
    {
        /// <summary>
        /// Gets the trait values from the Category attribute.
        /// </summary>
        /// <param name="traitAttribute">The trait attribute containing the trait values.</param>
        /// <returns>The trait values.</returns>
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            IEnumerable<object> ctorArgs = traitAttribute.GetConstructorArguments();
            Debug.Assert(ctorArgs.Count() >= 2);

            string issue = ctorArgs.First().ToString();
            TestPlatforms platforms = TestPlatforms.Any;
            TargetFrameworkMonikers frameworks = TargetFrameworkMonikers.Any;
            TestRuntimes runtimes = TestRuntimes.Any;
            Type calleeType = null;
            string[] conditionMemberNames = null;
            
            foreach (object arg in ctorArgs.Skip(1)) // First argument is the issue number.
            {
                if (arg is TestPlatforms)
                {
                    platforms = (TestPlatforms)arg;
                }
                else if (arg is TargetFrameworkMonikers)
                {
                    frameworks = (TargetFrameworkMonikers)arg;
                }
                else if (arg is TestRuntimes)
                {
                    runtimes = (TestRuntimes)arg;
                }
                else if (arg is Type)
                {
                    calleeType = (Type)arg;
                }
                else if (arg is string[])
                {
                    conditionMemberNames = (string[])arg;
                }
            }

            if (calleeType != null && conditionMemberNames != null)
            {
                if (!DiscovererHelpers.Evaluate(calleeType, conditionMemberNames))
                {
                    yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.Failing);
                }
            }        
            else if (DiscovererHelpers.TestPlatformApplies(platforms) &&
                DiscovererHelpers.TestRuntimeApplies(runtimes) &&
                DiscovererHelpers.TestFrameworkApplies(frameworks))
            {
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.Failing);
            }
        }
    }
}
