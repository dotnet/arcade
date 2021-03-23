// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// MSBuild task to generate a workload pack installer (MSI) from a NuGet package.
    /// </summary>
    public class GenerateMsi : GenerateMsiBase
    {
        /// <summary>
        /// The kind of package, e.g. framework, sdk, template, etc.
        /// </summary>
        [Required]
        public string Kind
        {
            get;
            set;
        }

        /// <summary>
        /// The target platforms to use for generating MSIs.
        /// </summary>
        [Required]
        public ITaskItem[] Platforms
        {
            get;
            set;
        }

        [Required]
        public string SourcePackage
        {
            get;
            set;
        }

        public override bool Execute()
        {
            try
            {
                if (!Enum.TryParse(Kind, true, out WorkloadPackKind kind))
                {
                    Log.LogError($"Invalid package kind ({Kind}).");
                }
                                
                string[] platforms = Platforms.Select(p => p.ItemSpec).ToArray();
                List<ITaskItem> msis = new();
                msis.AddRange(Generate(SourcePackage, OutputPath, GetInstallDir(kind), platforms));
                Msis = msis.ToArray();
            }
            catch (Exception e)
            {
                Log.LogMessage(MessageImportance.Low, e.StackTrace);
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
