// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    public abstract class GenerateTaskBase : Microsoft.Build.Utilities.Task
    {
        public const int MaxPayloadRelativePath = 182;

        public static readonly string[] SupportedVisualStudioPlatforms = { "x86", "x64" };

        /// <summary>
        /// The root intermediate output directory. 
        /// </summary>
        [Required]
        public string IntermediateBaseOutputPath
        {
            get;
            set;
        }

        /// <summary>
        /// Root directory for generated source files.
        /// </summary>
        public string SourceDirectory => Path.Combine(IntermediateBaseOutputPath, "src");

        /// <summary>
        /// Root directory for extracting package content.
        /// </summary>
        public string PackageDirectory => Path.Combine(IntermediateBaseOutputPath, "pkg");

        /// <summary>
        /// Root directory for generated SWIX projects.
        /// </summary>
        public string SwixDirectory => Path.Combine(SourceDirectory, "swix");

        /// <summary>
        /// Root directory for generated MSI sources.
        /// </summary>
        public string MsiDirectory => Path.Combine(SourceDirectory, "msi");

        /// <summary>
        /// Root directory for .csproj sources to build NuGet packages.
        /// </summary>
        public string MsiPackageDirectory => Path.Combine(SourceDirectory, "msiPackage");

        /// <summary>
        /// The directory containing the WiX toolset binaries.
        /// </summary>
        [Required]
        public string WixToolsetPath
        {
            get;
            set;
        }

        /// <summary>
        /// Determines if the specified platfor is support by Visual Studio.
        /// </summary>
        /// <param name="platform">The platform to check</param>
        /// <returns><see langword="true" /> if the platform is supported by Visual Studio.</returns>
        protected bool IsSupportedByVisualStudio(string platform)
        {
            return SupportedVisualStudioPlatforms.Contains(platform);
        }

        protected void CheckRelativePayloadPath(string relativePath)
        {
            if (relativePath.Length > MaxPayloadRelativePath)
            {
                // We'll let the task's execute method take care of logging this and terminating.
                throw new Exception($"Payload relative path exceeds max length ({MaxPayloadRelativePath}): {relativePath}");
            }
        }
    }
}
