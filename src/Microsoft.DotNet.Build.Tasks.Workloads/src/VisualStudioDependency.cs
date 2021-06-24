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

        public Version MinVersion
        {
            get;
        }

        public Version MaxVersion
        {
            get;
        }

        /// <summary>
        /// Creates a dependency with an exact version.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="version"></param>
        public VisualStudioDependency(string id, Version version) : this(id, version, version)
        {

        }
        
        /// <summary>
        /// Creates a dependency with a minimum and maximum versions.
        /// </summary>
        /// <param name="id">The Visual Studio package ID. The ID applies to packages, components, component groups, etc.</param>
        /// <param name="minVersion">The minimum required version, inclusive.</param>
        /// <param name="maxVersion">The maximum version, exclusive. May be <see langword="null"/> if there is only a minimum requirement. If
        /// equal to <paramref name="minVersion"/>, an exact version requirement is created, e.g. [1.2.0].</param>
        public VisualStudioDependency(string id, Version minVersion, Version maxVersion)
        {
            Id = id;
            MinVersion = minVersion;
            MaxVersion = maxVersion;
        }

        public string GetVersion()
        {
            if ((MaxVersion != null) && (MinVersion == MaxVersion))
            {
                return $"[{MinVersion}]";
            }

            if (MaxVersion == null)
            {
                return $"[{MinVersion},)";
            }

            return $"[{MinVersion},{MaxVersion})";
        }
    }
}
