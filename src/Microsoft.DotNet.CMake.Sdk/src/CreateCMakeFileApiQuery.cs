// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;

namespace Microsoft.DotNet.CMake.Sdk
{
    /// <summary>
    /// Creates a CMake File API query file to request codemodel information.
    /// </summary>
    public class CreateCMakeFileApiQuery : Task
    {
        /// <summary>
        /// The CMake build output directory where the query should be created.
        /// </summary>
        [Required]
        public string CMakeOutputDir { get; set; }

        public override bool Execute()
        {
            try
            {
                string queryDir = Path.Combine(CMakeOutputDir, ".cmake", "api", "v1", "query");
                Directory.CreateDirectory(queryDir);

                string queryFile = Path.Combine(queryDir, "codemodel-v2");
                
                // Create an empty file to request codemodel-v2 information
                File.WriteAllText(queryFile, string.Empty);
                
                Log.LogMessage(MessageImportance.Low, $"Created CMake File API query at: {queryFile}");
                
                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
                return false;
            }
        }
    }
}
