// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// Defines well-known package identifiers for WiX toolset packages
    /// </summary>
    public class ToolsetPackages
    {
        /// <summary>
        /// Provides access to Heat tool for harvesting directories, files, etc.
        /// </summary>
        public const string MicrosoftWixToolsetHeat = "Microsoft.WixToolset.Heat";

        /// <summary>
        /// Provides access to the Util extension, including built-in custom actions.
        /// </summary>
        public const string MicrosoftWixToolsetUtilExtension = "Microsoft.WixToolset.Util.wixext";

        /// <summary>
        /// Provides access to UI extensions like custom dialog sets for MSIs.
        /// </summary>
        public const string MicrosoftWixToolsetUIExtension = "Microsoft.WixToolset.UI.wixext";

        /// <summary>
        /// Provides the dependency provider extension to manage shared installations and MSI reference counting.
        /// </summary>
        public const string MicrosoftWixToolsetDependencyExtension = "Microsoft.WixToolset.Dependency.wixext";
    }
}
