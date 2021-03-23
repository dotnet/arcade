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
    public abstract class GenerateTaskBase : Task
    {
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

        public string SwixDirectory => Path.Combine(SourceDirectory, "swix");

        public string MsiDirectory => Path.Combine(SourceDirectory, "msi");

        /// <summary>
        /// The directory containing the WiX toolset binaries.
        /// </summary>
        [Required]
        public string WixToolsetPath
        {
            get;
            set;
        }
    }
}
