// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Build.Tasks.Workloads.Msi
{
    /// <summary>
    /// Defines token names used to create MSIs.
    /// </summary>
    internal class MsiTokens
    {
        public static readonly string __DIR_REF_ID__ = nameof(__DIR_REF_ID__);
        public static readonly string __DIR_ID__ = nameof(__DIR_ID__);
        public static readonly string __DIR_NAME__ = nameof(__DIR_NAME__);

        /// <summary>
        /// Replacement token for Files@Include.
        /// </summary>
        public static readonly string __INCLUDE__ = nameof(__INCLUDE__);

        /// <summary>
        /// Replacement token for ComponentGroup@Id.
        /// </summary>
        public static readonly string __COMPONENT_GROUP_ID__ = nameof(__COMPONENT_GROUP_ID__);
    }
}
