// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("get-subscriptions", HelpText = "Get information about subscriptions.")]
    class GetSubscriptionsCommandLineOptions : CommandLineOptions
    {
        [Option("target-repo", HelpText = "Filter by target repo (matches substring).")]
        public string TargetRepository { get; set; }

        [Option("source-repo", HelpText = "Filter by source repo (matches substring).")]
        public string SourceRepository { get; set; }

        [Option("channel", HelpText = "Filter by source channel (matches substring).")]
        public string Channel { get; set; }

        [Option("target-branch", HelpText = "Filter by target branch (matches substring).")]
        public string TargetBranch { get; set; }

        public override Operation GetOperation()
        {
            return new GetSubscriptionsOperation(this);
        }
    }
}
