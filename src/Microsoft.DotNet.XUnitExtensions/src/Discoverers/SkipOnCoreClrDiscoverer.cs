using System;
using System.Collections;
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
            if (!SkipOnMonoDiscoverer.IsMonoRuntime)
            {
                TestPlatforms testPlatforms = TestPlatforms.Any;
                RuntimeStressTestModes stressMode = RuntimeStressTestModes.Any;
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
                    if (IsCheckedRuntime() || IsRuntimeStressTesting)
                    {
                        if (StressModeApplies(stressMode))
                        {
                            return new[] { new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.Failing) };
                        }
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

            if (stressMode.HasFlag(RuntimeStressTestModes.GCStress3) && IsGCStress3)
            {
                return true;
            }

            if (stressMode.HasFlag(RuntimeStressTestModes.GCStressC) && IsGCStressC)
            {
                return true;
            }

            if (stressMode.HasFlag(RuntimeStressTestModes.ZapDisable) && IsZapDisable)
            {
                return true;
            }

            if (stressMode.HasFlag(RuntimeStressTestModes.TailcallStress) && IsTailCallStress)
            {
                return true;
            }

            if (stressMode.HasFlag(RuntimeStressTestModes.JitStressRegs) && IsJitStressRegs)
            {
                return true;
            }

            if (stressMode.HasFlag(RuntimeStressTestModes.JitStress) && IsJitStress)
            {
                return true;
            }

            if (stressMode.HasFlag(RuntimeStressTestModes.JitMinOpts) && IsJitMinOpts)
            {
                return true;
            }

            return false;
        }

        private bool IsRuntimeStressTesting =>
            IsGCStress3 ||
            IsGCStressC ||
            IsZapDisable ||
            IsTailCallStress ||
            IsJitStressRegs ||
            IsJitStress ||
            IsJitMinOpts;

        private string GetEnvironmentVariableValue(string name) => Environment.GetEnvironmentVariable(name) ?? "0";

        private bool IsCheckedRuntime()
        {
            Assembly assembly = typeof(string).Assembly;
            AssemblyConfigurationAttribute assemblyConfigurationAttribute = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
            if (assemblyConfigurationAttribute != null)
            {
                if (string.Equals(assemblyConfigurationAttribute.Configuration, "Checked", StringComparison.InvariantCulture))
                    return true;
            }

            return false;
        }

        private bool IsJitStress => !string.Equals(GetEnvironmentVariableValue("COMPlus_JitStress"), "0", StringComparison.InvariantCulture);

        private bool IsJitStressRegs => !string.Equals(GetEnvironmentVariableValue("COMPlus_JitStressRegs"), "0", StringComparison.InvariantCulture);

        private bool IsJitMinOpts => string.Equals(GetEnvironmentVariableValue("COMPlus_JitMinOpts"), "1", StringComparison.InvariantCulture);

        private bool IsTailCallStress => string.Equals(GetEnvironmentVariableValue("COMPlus_TailcallStress"), "1", StringComparison.InvariantCulture);

        private bool IsZapDisable => string.Equals(GetEnvironmentVariableValue("COMPlus_ZapDisable"), "1", StringComparison.InvariantCulture);

        private bool IsGCStress3 => string.Equals(GetEnvironmentVariableValue("COMPlus_GCStress"), "0x3", StringComparison.InvariantCulture);

        private bool IsGCStressC => string.Equals(GetEnvironmentVariableValue("COMPlus_GCStress"), "0xC", StringComparison.InvariantCulture);
    }
}
