using System;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Git
{
    public class GitCommitHash : ToolTask
    {
        [Output]
        public string HashValue { get; set; }
        protected override string ToolName => "git.exe";

        protected override string GenerateFullPathToTool()
        {
            return $@"git";
        }

        protected override string GenerateCommandLineCommands()
        {
            return "rev-parse HEAD";
        }

        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            HashValue = singleLine;
        }
    }
}
