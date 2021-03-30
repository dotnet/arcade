// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// A tool task to invoke the WiX harvesting tool (Heat.exe).
    /// </summary>
    public class HarvestToolTask : WixToolTask
    {
        private static readonly Dictionary<HeatSuppressions, string> s_SuppressionArguments = new()
        {
            { HeatSuppressions.SuppressComElements, "-scom" },
            { HeatSuppressions.SuppressFragments, "-sfrag" },
            { HeatSuppressions.SuppressRootDirectory, "-srd" },
            { HeatSuppressions.SuppressRegistryHarvesting, "-sreg" },
            { HeatSuppressions.SuppressUuid, "-suid" },
            { HeatSuppressions.SuppressVb6Com, "-svb6" }
        };

        /// <summary>
        /// The name of the component group to generate for the harvested content.
        /// </summary>
        public string ComponentGroupName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of the directory reference pointing to root directories. The name cannot contain any spaces.
        /// </summary>
        public string DirectoryReference
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets how GUIDs are generated. The default is <see cref="GuidOptions.GenerateAtCompileTime"/>.
        /// </summary>
        public GuidOptions GenerateGuids
        {
            get;
            set;
        } = GuidOptions.GenerateAtCompileTime;

        /// <summary>
        /// The fully qualified path of the output file to generate.
        /// </summary>
        public string OutputFile
        {
            get;
            set;
        }        

        /// <summary>
        /// The source directory to harvest.
        /// </summary>
        public string SourceDirectory
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or set suppressions for Heat. The default setting suppress registry harvesting (-sreg) and root directory elements (-srd).
        /// </summary>
        public HeatSuppressions Suppressions
        {
            get;
            set;
        } = HeatSuppressions.SuppressRegistryHarvesting | HeatSuppressions.SuppressRootDirectory;

        protected override string ToolName => "heat.exe";

        /// <summary>
        /// Executes "heat.exe" to harvest a directory and produce a WiX source file (.wxs).
        /// </summary>
        /// <param name="wixToolsetPath">The fully qualified path to the WiX toolset.</param>
        /// <param name="sourceDirectory">The path of the directory to harvest.</param>
        /// <param name="outputFile">The fully qualified path of the output file to generate.</param>
        public HarvestToolTask(IBuildEngine engine, string wixToolsetPath) : base(engine, wixToolsetPath)
        {
        }

        protected override string GenerateCommandLineCommands()
        {
            // Harvesting type (command noun) must be the first argument.
            CommandLineBuilder.AppendSwitchIfNotNull("dir ", SourceDirectory);

            CommandLineBuilder.AppendSwitchIfNotNull("-cg ", ComponentGroupName);

            // Override File/@Source="SourceDir" with a preprocessor variable, $(var.SourceDir)
            CommandLineBuilder.AppendSwitch("-var var.SourceDir");

            // GUID generation 
            if (GenerateGuids == GuidOptions.GenerateAtCompileTime)
            {
                CommandLineBuilder.AppendSwitch("-ag");
            }
            else if (GenerateGuids == GuidOptions.GenerateNow)
            {
                CommandLineBuilder.AppendSwitch("-gg");
            }

            // Suppressions
            foreach (var suppression in s_SuppressionArguments.Keys)
            {
                if ((Suppressions & suppression) != 0)
                {
                    CommandLineBuilder.AppendSwitch(s_SuppressionArguments[suppression]);
                }
            }

            CommandLineBuilder.AppendSwitchIfNotNull("-dr ", DirectoryReference);
            CommandLineBuilder.AppendSwitchIfNotNull("-o ", OutputFile);

            return CommandLineBuilder.ToString();
        }
    }
}
