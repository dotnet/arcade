// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// A collection of compiler extension assemblies to use.
        /// </summary>
        public ICollection<string> Extensions
        {
            get;
        } = new List<string>();

        public string OutputFile
        {
            get;
            set;
        }

        public IEnumerable<string> SourceFiles
        {
            get;
            set;
        }

        /// <summary>
        /// Semicolon sepearate list of ICEs to suppress.
        /// </summary>
        public string SuppressIces
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
            CommandLineBuilder.AppendSwitchIfNotNull("-sice:", SuppressIces);
            CommandLineBuilder.AppendFileNamesIfNotNull(SourceFiles.ToArray(), " ");

            return CommandLineBuilder.ToString();
        }
    }
}
