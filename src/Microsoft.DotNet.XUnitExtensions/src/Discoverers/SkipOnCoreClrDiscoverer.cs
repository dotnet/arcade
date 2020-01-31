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
                RuntimeTestModes stressMode = RuntimeTestModes.Any;
                foreach (object arg in traitAttribute.GetConstructorArguments().Skip(1)) // We skip the first one as it is the reason
                {
                    if (arg is TestPlatforms tp)
                    {
                        testPlatforms = tp;
                    }
                    else if (arg is RuntimeTestModes rstm)
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
        private static bool StressModeApplies(RuntimeTestModes stressMode) =>
            stressMode == RuntimeTestModes.Any ||
            (stressMode.HasFlag(RuntimeTestModes.CheckedRuntime) && s_isCheckedRuntime.Value) ||
            (stressMode.HasFlag(RuntimeTestModes.GCStress3) && s_isGCStress3.Value) ||
            (stressMode.HasFlag(RuntimeTestModes.GCStressC) && s_isGCStressC.Value) ||
            (stressMode.HasFlag(RuntimeTestModes.ZapDisable) && s_isZapDisable.Value) ||
            (stressMode.HasFlag(RuntimeTestModes.TailcallStress) && s_isTailCallStress.Value) ||
            (stressMode.HasFlag(RuntimeTestModes.JitStressRegs) && s_isJitStressRegs.Value) ||
            (stressMode.HasFlag(RuntimeTestModes.JitStress) && s_isJitStress.Value) ||
            (stressMode.HasFlag(RuntimeTestModes.JitMinOpts) && s_isJitMinOpts.Value);

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
