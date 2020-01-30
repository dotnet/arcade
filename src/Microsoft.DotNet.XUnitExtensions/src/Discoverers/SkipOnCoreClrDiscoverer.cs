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
        private static readonly Lazy<bool> s_isJitStress = new Lazy<bool>(() => !string.Equals(GetEnvironmentVariableValue("COMPlus_JitStress"), "0", StringComparison.InvariantCulture));
        private static readonly Lazy<bool> s_isJitStressRegs = new Lazy<bool>(() => !string.Equals(GetEnvironmentVariableValue("COMPlus_JitStressRegs"), "0", StringComparison.InvariantCulture));
        private static readonly Lazy<bool> s_isJitMinOpts = new Lazy<bool>(() => string.Equals(GetEnvironmentVariableValue("COMPlus_JITMinOpts"), "1", StringComparison.InvariantCulture));
        private static readonly Lazy<bool> s_isTailCallStress = new Lazy<bool>(() => string.Equals(GetEnvironmentVariableValue("COMPlus_TailcallStress"), "1", StringComparison.InvariantCulture));
        private static readonly Lazy<bool> s_isZapDisable = new Lazy<bool>(() => string.Equals(GetEnvironmentVariableValue("COMPlus_ZapDisable"), "1", StringComparison.InvariantCulture));
        private static readonly Lazy<bool> s_isGCStress3 = new Lazy<bool>(() => CompareGCStressModeAsLower(GetEnvironmentVariableValue("COMPlus_GCStress"), "0x3", "3"));
        private static readonly Lazy<bool> s_isGCStressC = new Lazy<bool>(() => CompareGCStressModeAsLower(GetEnvironmentVariableValue("COMPlus_GCStress"), "0xC", "C"));
        private static readonly Lazy<bool> s_isCheckedRuntime = new Lazy<bool>(() => IsCheckedRuntime());

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

                if (DiscovererHelpers.TestPlatformApplies(testPlatforms) && StressModeApplies(stressMode))
                {
                    return new[] { new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.Failing) };
                }
            }

            return Array.Empty<KeyValuePair<string, string>>();
        }

        // Order here matters as some env variables may appear in multiple modes
        private static bool StressModeApplies(RuntimeStressTestModes stressMode) =>
            stressMode == RuntimeStressTestModes.Any ||
            (stressMode.HasFlag(RuntimeStressTestModes.CheckedRuntime) && s_isCheckedRuntime.Value) ||
            (stressMode.HasFlag(RuntimeStressTestModes.GCStress3) && s_isGCStress3.Value) ||
            (stressMode.HasFlag(RuntimeStressTestModes.GCStressC) && s_isGCStressC.Value) ||
            (stressMode.HasFlag(RuntimeStressTestModes.ZapDisable) && s_isZapDisable.Value) ||
            (stressMode.HasFlag(RuntimeStressTestModes.TailcallStress) && s_isTailCallStress.Value) ||
            (stressMode.HasFlag(RuntimeStressTestModes.JitStressRegs) && s_isJitStressRegs.Value) ||
            (stressMode.HasFlag(RuntimeStressTestModes.JitStress) && s_isJitStress.Value) ||
            (stressMode.HasFlag(RuntimeStressTestModes.JitMinOpts) && s_isJitMinOpts.Value);

        private static string GetEnvironmentVariableValue(string name) => Environment.GetEnvironmentVariable(name) ?? "0";

        private static bool IsCheckedRuntime()
        {
            Assembly assembly = typeof(string).Assembly;
            AssemblyConfigurationAttribute assemblyConfigurationAttribute = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();

            return assemblyConfigurationAttribute != null &&
                string.Equals(assemblyConfigurationAttribute.Configuration, "Checked", StringComparison.InvariantCulture);
        }

        private static bool CompareGCStressModeAsLower(string value, string first, string second)
        {
            value = value.ToLowerInvariant();
            return string.Equals(value, first.ToLowerInvariant(), StringComparison.InvariantCulture) ||
                string.Equals(value, second.ToLowerInvariant(), StringComparison.InvariantCulture) ||
                string.Equals(value, "0xf", StringComparison.InvariantCulture) ||
                string.Equals(value, "f", StringComparison.InvariantCulture);
        }
    }
}
