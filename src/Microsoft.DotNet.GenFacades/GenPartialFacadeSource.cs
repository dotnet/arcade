// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;
using System;
using System.Linq;

namespace Microsoft.DotNet.GenFacades
{
    public class GenPartialFacadeSource : BuildTask
    {
        [Required]
        public ITaskItem[] ReferencePaths { get; set; }

        [Required]
        public string ReferenceAssembly { get; set; }

        public ITaskItem[] CompileFiles { get; set; }

        public string DefineConstants { get; set; }

        public bool IgnoreMissingTypes { get; set; }

        public string[] IgnoreMissingTypesList { get; set; }

        public string[] OmitTypes { get; set; }

        public ITaskItem[] SeedTypePreferences { get; set; }

        [Required]
        public string OutputSourcePath { get; set; }
        
        public override bool Execute()
        {
            bool result = true;
            try
            {
                result = GenPartialFacadeSourceGenerator.Execute(
                    ReferencePaths?.Select(item => item.ItemSpec).ToArray(),
                    ReferenceAssembly,
                    CompileFiles?.Select(item => item.ItemSpec).ToArray(),
                    DefineConstants,
                    OutputSourcePath,
                    Log,
                    IgnoreMissingTypes,
                    IgnoreMissingTypesList,
                    OmitTypes,
                    SeedTypePreferences);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, showStackTrace: false);
            }

            return result && !Log.HasLoggedErrors;
        }
    }
}
