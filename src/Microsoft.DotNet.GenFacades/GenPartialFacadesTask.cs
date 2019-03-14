// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.GenFacades
{
    public class GenPartialFacadesTask : Task
    {
        [Required]
        public ITaskItem[] ReferencePaths { get; set; }

        [Required]
        public string ReferenceAssembly { get; set; }

        public ITaskItem[] CompileFiles { get; set; }

        public string DefineConstants { get; set; }

        public bool IgnoreMissingTypes { get; set; }

        public string InclusionContracts { get; set; }

        [Required]
        public string AssemblyName { get; set; }

        public ITaskItem[] SeedTypePreferences { get; set; }

        [Required]
        public string OutputSourcePath { get; set; }
        
        public override bool Execute()
        {
            TraceLogger logger = new TraceLogger(Log);

            try
            {
                Trace.Listeners.Add(logger);

                string[] seedTypePreferencesUnsplit = null;
                if (SeedTypePreferences != null)
                {
                    seedTypePreferencesUnsplit = SeedTypePreferences.Select(iti => $"{iti.ItemSpec}={iti.GetMetadata("Aliases")}").ToArray();
                    Trace.WriteLine("seedTypePreferences: " + string.Join(" || ", seedTypePreferencesUnsplit));
                }

                bool error = GenPartialFacadesGenerator.Execute(
                    ReferencePaths.Select(item => item.ItemSpec).ToArray(),
                    ReferenceAssembly,
                    CompileFiles.Select(item => item.ItemSpec).ToArray(),
                    AssemblyName,
                    DefineConstants,
                    OutputSourcePath,
                    IgnoreMissingTypes,
                    seedTypePreferencesUnsplit);

                if (error)
                {
                    Log.LogError("Errors were encountered when generating facade(s).");
                }

                return error;
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, showStackTrace: false);
                return false;
            }
            finally
            {
                Trace.Listeners.Remove(logger);
            }
        }
    }
}
