using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Git
{
    public class GitBranch : ToolTask
    {
        [Output]
        public string BranchName { get; set; }

        protected override string ToolName => (Environment.OSVersion.Platform == PlatformID.Unix) ? "git" : "git.exe";

        protected override string GenerateFullPathToTool()
        {
            // Lets search for this tool...
            return $@"git";
        }

        protected override string GenerateCommandLineCommands()
        {
            return "rev-parse --abbrev-ref HEAD";
        }

        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            BranchName = singleLine;
        }
    }
}
