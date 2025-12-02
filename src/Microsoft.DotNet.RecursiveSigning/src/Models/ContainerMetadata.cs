// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Format-specific metadata for a container.
    /// </summary>
    public sealed class ContainerMetadata
    {
        /// <summary>
        /// Format-specific properties to preserve.
        /// </summary>
        public Dictionary<string, object> Properties { get; }

        public ContainerMetadata()
        {
            Properties = new Dictionary<string, object>();
        }
    }
}
