// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Build.Tasks.Workloads.Wix
{
    /// <summary>
    /// Property names used inside a WiX project (.wixproj).
    /// </summary>
    internal class WixProperties
    {
        /// <summary>
        /// The platform of the installer being built.
        /// </summary>
        public static readonly string InstallerPlatform = nameof(InstallerPlatform);

        /// <summary>
        /// Turns off validation (ICE) when set to true.
        /// </summary>
        public static readonly string SuppressValidation = nameof(SuppressValidation);

        /// <summary>
        /// The name of the output produced by the .wixproj. The extension is determined by the WiX SDK
        /// based on the output type.
        /// </summary>
        public static readonly string TargetName = nameof(TargetName);

        /// <summary>
        /// The type of output produced by the project, for example, Package produces an MSI, Patch produces an MSP, etc.
        /// </summary>
        public static readonly string OutputType = nameof(OutputType);

        /// <summary>
        /// The debug information to emit.
        /// </summary>
        public static readonly string DebugType = nameof(DebugType);

        /// <summary>
        /// Boolean property indicating whether to generate WiX pack used for signing.
        /// </summary>
        public static readonly string GenerateWixpack = nameof(GenerateWixpack);
    }
}
