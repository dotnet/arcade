// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.IO.Internal;

namespace Microsoft.DotNet.Build.Tasks.IO
{
    /// <summary>
    /// Changes Unix permissions on files using  chmod . On Windows, this task is a no-op.
    /// </summary>
    public class Chmod : ToolTask
    {
        public Chmod()
        {
            LogStandardErrorAsError = true;
        }

        /// <summary>
        /// The file to be chmod-ed.
        /// </summary>
        [Required]
        public string File { get; set; }

        /// <summary>
        /// The file mode to be used. Mode can be any input supported my `man chmod`. e.g. <c>+x</c>, <c>0755</c>
        /// </summary>
        [Required]
        public string Mode { get; set; }

        protected override bool SkipTaskExecution()
        {
#if NET45
            return true;
#elif NETCOREAPP2_0
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#else
#error Update target frameworks
#endif
        }

        // increase the default output importance from Low to Normal
        protected override MessageImportance StandardOutputLoggingImportance { get; } = MessageImportance.Normal;

        protected override MessageImportance StandardErrorLoggingImportance { get; } = MessageImportance.Normal;

        protected override string ToolName { get; } = "chmod";

        protected override string GenerateFullPathToTool() => ToolName;

        protected override string GenerateCommandLineCommands()
        {
            return $"{Mode} {ArgumentEscaper.EscapeSingleArg(File)}";
        }
    }
}
