// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
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

        /// <summary>
        /// The path of the NuGet package to convert into an MSI.
        /// </summary>
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
                    return false;
                }

                string[] platforms = Platforms.Select(p => p.ItemSpec).ToArray();
                IEnumerable<string> unsupportedPlatforms = platforms.Except(SupportedPlatforms);

                if (unsupportedPlatforms.Count() > 0)
                {
                    Log.LogError($"Unsupported platforms detected: {String.Join(",", unsupportedPlatforms)}.");
                    return false;
                }

                // For a single MSI we always generate all platforms and simply use the ID of the source package for
                // the SWIX projects.
                List<ITaskItem> msis = new();
                msis.AddRange(Generate(SourcePackage, null, OutputPath, kind, platforms));
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
