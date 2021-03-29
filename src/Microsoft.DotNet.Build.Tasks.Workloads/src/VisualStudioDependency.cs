// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// Represents a Visual Studio setup dependency.
    /// </summary>
    public class VisualStudioDependency
    {
        /// <summary>
        /// The identifier of the dependent (package, component, etc.).
        /// </summary>
        public string Id
        {
            get;            
        }

        /// <summary>
        /// The version of the dependent.
        /// </summary>
        public Version Version
        {
            get;
        }

        public VisualStudioDependency(string id, Version version)
        {
            Id = id;
            Version = version;
        }
    }
}
