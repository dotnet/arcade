using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.DotNet.XUnitExtensions
{
    public class SkipOnCoreClrDiscoverer : ITraitDiscoverer
    {
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            if (IsCheckedRuntime())
            {
                TestPlatforms testPlatforms = TestPlatforms.Any;
                RuntimeStressTestModes stressMode = 0;

                foreach (object arg in traitAttribute.GetConstructorArguments().Skip(1)) // We skip the first one as it is the reason
                {
                    if (arg is TestPlatforms tp)
                    {
                        testPlatforms = tp;
                    }
                    else if (arg is RuntimeStressTestModes rstm)
                    {
                        stressMode = rstm;
                    }
                }

                if (DiscovererHelpers.TestPlatformApplies(testPlatforms))
                {
                    if (StressModeApplies(stressMode))
                    {
                        return new[] { new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.Failing) };
                    }
                }
            }

            return Array.Empty<KeyValuePair<string, string>>();
        }

        private bool StressModeApplies(RuntimeStressTestModes stressMode)
        {
            if (stressMode == 0)
            {
                return true;
            }

            if (stressMode.HasFlag(RuntimeStressTestModes.ZapDisable) && string.Equals(GetEnvironmentVariableValue("COMPlus_ZapDisable"), "1", StringComparison.InvariantCulture))
            {
                return true;
            }

            if (stressMode.HasFlag(RuntimeStressTestModes.TailcallStress) && string.Equals(GetEnvironmentVariableValue("COMPlus_TailcallStress"), "1", StringComparison.InvariantCulture))
            {
                return true;
            }

            if (stressMode.HasFlag(RuntimeStressTestModes.JitStressRegs) && !string.Equals(GetEnvironmentVariableValue("COMPlus_JitStressRegs"), "0", StringComparison.InvariantCulture))
            {
                return true;
            }

            if (stressMode.HasFlag(RuntimeStressTestModes.JitStress) && !string.Equals(GetEnvironmentVariableValue("COMPlus_JitStress"), "0", StringComparison.InvariantCulture))
            {
                return true;
            }

            if (stressMode.HasFlag(RuntimeStressTestModes.JitMinOpts) && string.Equals(GetEnvironmentVariableValue("COMPlus_JitMinOpts"), "1", StringComparison.InvariantCulture))
            {
                return true;
            }

            return false;
        }

        private string GetEnvironmentVariableValue(string name) => Environment.GetEnvironmentVariable(name) ?? "0";

        private bool IsCheckedRuntime()
        {
            if (!SkipOnMonoDiscoverer.IsMonoRuntime)
            {
                Assembly assembly = typeof(string).Assembly;
                AssemblyConfigurationAttribute assemblyConfigurationAttribute = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
                if (assemblyConfigurationAttribute != null)
                {
                    if (string.Equals(assemblyConfigurationAttribute.Configuration, "Checked", StringComparison.InvariantCulture))
                        return true;
                }
            }

            return false;
        }
    }
}
