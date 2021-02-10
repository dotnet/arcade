// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.DotNet.ApiCompat
{
    public class ApiCompatTask : BuildTask
    {
        [Required]
        public ITaskItem[] Contracts { get; set; }

        [Required]
        public ITaskItem[] ImplDirs { get; set; }

        public string RightOperand { get; set; }

        public string LeftOperand { get; set; }

        public string OutFilePath { get; set; }

        public ITaskItem[] BaselineFiles { get; set; }

        public bool ValidateBaseline { get; set; }

        public bool ResolveFx { get; set; }

        public bool SkipUnifyToLibPath { get; set; }

        public ITaskItem[] ContractDepends { get; set; }

        public string ContractCoreAssembly { get; set; }

        public bool IgnoreDesignTimeFacades { get; set; }

        public bool WarnOnMissingAssemblies { get; set; }

        public bool RespectInternals { get; set; }

        public bool WarnOnIncorrectVersion { get; set; }

        public bool EnforceOptionalRules { get; set; }

        public bool MDIL { get; set; }

        public bool ExcludeNonBrowsable { get; set; }

        public bool ExcludeCompilerGenerated { get; set; }

        public string RemapFile { get; set; }

        public bool SkipGroupByAssembly { get; private set; }

        public ITaskItem[] ExcludeAttributes { get; set; }

        public bool AllowDefaultInterfaceMethods { get; set; }

        [Output]
        public int ExitCode { get; set; }

        public override bool Execute()
        {
            TextWriter writer = string.IsNullOrWhiteSpace(OutFilePath) ?
                new MSBuildTextWriter(Log) :
                Executor.GetOutput(OutFilePath);

            ExitCode = Executor.Run(isMSBuildTask: true,
                Contracts.Select(c => c.ItemSpec),
                ImplDirs.Select(i => i.ItemSpec),
                writer,
                RightOperand,
                LeftOperand,
                listRules: false,
                BaselineFiles?.Select(b => b.ItemSpec),
                ValidateBaseline,
                ResolveFx,
                SkipUnifyToLibPath,
                ContractDepends?.Select(c => c.ItemSpec),
                ContractCoreAssembly,
                IgnoreDesignTimeFacades,
                WarnOnMissingAssemblies,
                RespectInternals,
                WarnOnIncorrectVersion,
                EnforceOptionalRules,
                MDIL,
                ExcludeNonBrowsable,
                ExcludeCompilerGenerated,
                RemapFile,
                SkipGroupByAssembly,
                ExcludeAttributes?.Select(e => e.ItemSpec),
                AllowDefaultInterfaceMethods);

            return !Log.HasLoggedErrors;
        }
    }
}
