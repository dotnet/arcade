// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// MSBuild task for generating a Visual Studio manifest project (.vsmanproj). The generated project can be used
    /// to create a manifest (.vsman) by merging JSON manifest files produced from one or more SWIX project.
    /// </summary>
    public class GenerateVisualStudioManifest : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// The base path where the project source will be generated.
        /// </summary>
        [Required]
        public string IntermediateOutputBase
        {
            get;
            set;
        }

        internal string IntermediateOutputPath => Path.Combine(IntermediateOutputBase, ManifestName);

        [Required]
        public string ManifestName
        {
            get;
            set;
        }

        [Required]
        public string ProductFamily
        {
            get;
            set;
        }

        [Required]
        public string ProductVersion
        {
            get;
            set;
        }

        [Required]
        public string ProductName
        {
            get;
            set;
        }

        [Output]
        public string GeneratedManifestProject
        {
            get;
            set;
        }

        public override bool Execute()
        {
            try
            {
                GeneratedManifestProject = EmbeddedTemplates.Extract("manifest.vsmanproj", IntermediateOutputPath, ManifestName + ".vsmanproj");
                Utils.StringReplace(GeneratedManifestProject, GetReplacementTokens(), Encoding.UTF8);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }

        private Dictionary<string, string> GetReplacementTokens()
        {
            return new Dictionary<string, string>()
            {
                {"__PRODUCT_NAME__", ProductName },
                {"__PRODUCT_FAMILY__", ProductFamily },
                {"__PRODUCT_VERSION__", ProductVersion },
            };
        }
    }
}
