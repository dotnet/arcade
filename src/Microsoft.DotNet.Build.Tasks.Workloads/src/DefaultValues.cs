// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// Defines default values that can be used to when creating workload artifacts.
    /// </summary>
    internal static class DefaultValues
    {
        /// <summary>
        /// The default category to assign to a SWIX component. The value is used
        /// to group individual components in Visual Studio Installer.
        /// </summary>
        public static readonly string ComponentCategory = ".NET";

        /// <summary>
        /// The default value to assign to the Manufacturer property of an MSI.
        /// </summary>
        public static readonly string Manufacturer = "Microsoft Corporation";
    }
}
