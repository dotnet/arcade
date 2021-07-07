// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    public class CompileToolTask : WixToolTask
    {
        /// <summary>
        /// The default architecture for a package, components, etc.
        /// </summary>
        public string Arch
        {
            get;
            set;
        } = "x86";

        /// <summary>
        /// A collection of WiX extension assemblies to use.
        /// </summary>
        public ICollection<string> Extensions
        {
            get;
        } = new List<string>();

        public string OutputPath
        {
            get;
            set;
        }

        public IEnumerable<string> SourceFiles
        {
            get;
            set;
        }

        protected override string ToolName => "candle.exe";

        public CompileToolTask(IBuildEngine engine, string wixToolsetPath) : base(engine, wixToolsetPath)
        {

        }

        protected override string GenerateCommandLineCommands()
        {
            CommandLineBuilder.AppendSwitchIfNotNull("-out ", OutputPath);
            // No trailing space, preprocessor definitions are passed as -d<Variable>=<Value>
            CommandLineBuilder.AppendArrayIfNotNull("-d", PreprocessorDefinitions.ToArray());
            CommandLineBuilder.AppendArrayIfNotNull("-ext ", Extensions.ToArray());
            CommandLineBuilder.AppendSwitchIfNotNull("-arch ", Arch);
            CommandLineBuilder.AppendFileNamesIfNotNull(SourceFiles.ToArray(), " ");
            return CommandLineBuilder.ToString();
        }
    }
}
