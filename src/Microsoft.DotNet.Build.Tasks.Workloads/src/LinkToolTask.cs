// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    public class LinkToolTask : WixToolTask
    {
        protected override string ToolName => "light.exe";

        public bool AddFileVersion
        {
            get;
            set;
        } = true;

        public string OutputFile
        {
            get;
            set;
        }

        /// <summary>
        /// A collection of compiler extension assemblies to use.
        /// </summary>
        public ICollection<string> Extensions
        {
            get;
        } = new List<string>();

        public IEnumerable<string> SourceFiles
        {
            get;
            set;
        }

        public LinkToolTask(IBuildEngine engine, string wixToolsetPath) : base(engine, wixToolsetPath)
        {

        }

        protected override string GenerateCommandLineCommands()
        {
            CommandLineBuilder.AppendSwitchIfNotNull("-o ", OutputFile);
            CommandLineBuilder.AppendSwitchIfTrue("-fv", AddFileVersion);
            CommandLineBuilder.AppendArrayIfNotNull("-ext ", Extensions.ToArray());
            CommandLineBuilder.AppendFileNamesIfNotNull(SourceFiles.ToArray(), " ");

            return CommandLineBuilder.ToString();
        }
    }
}
