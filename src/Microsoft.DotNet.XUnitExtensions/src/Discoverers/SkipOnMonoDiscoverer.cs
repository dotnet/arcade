using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.DotNet.XUnitExtensions
{
    public class SkipOnMonoDiscoverer : ITraitDiscoverer
    {
        private static readonly Lazy<bool> s_isMonoRuntime = new Lazy<bool>(() => Type.GetType("Mono.RuntimeStructs") != null);

        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            if (IsMonoRuntime)
            {
                TestPlatforms testPlatforms = TestPlatforms.Any;

                // Last argument is either the TestPlatform or the test platform to skip the test on.
                if (traitAttribute.GetConstructorArguments().LastOrDefault() is TestPlatforms tp)
                {
                    testPlatforms = tp;
                }

                if (DiscovererHelpers.TestPlatformApplies(testPlatforms))
                {
                    return new[] { new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.Failing) };
                }
            }

            return Array.Empty<KeyValuePair<string, string>>();
        }

        public static bool IsMonoRuntime => s_isMonoRuntime.Value;
    }
}
