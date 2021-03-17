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

        public string SourceDirectory => Path.Combine(IntermediateBaseOutputPath, "src");

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
