// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;

namespace Microsoft.DotNet.Build.Tasks.Versioning
{
    internal class GitInfo : ToolTask
    {
        protected override string ToolName { get; } = "git";

        protected override string GenerateFullPathToTool() => ToolName;

        [Output]
        public string HeadCommitSHA { get; set; } = String.Empty;

        [Output]
        public DateTime HeadCommitDate { get; set; }

        protected override string GenerateCommandLineCommands()
        {
            return "log -1 --format=\"%h %cI\"";
        }

        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            if (String.IsNullOrEmpty(singleLine)) return;

            int separator = singleLine.IndexOf(' ');

            if (separator < 0) return;

            HeadCommitSHA = singleLine.Substring(0, separator + 1);

            if (DateTime.TryParse(singleLine.Substring(separator), out var ParsedDate))
            {
                HeadCommitDate = ParsedDate.ToUniversalTime();
            }
        }
    }
}
