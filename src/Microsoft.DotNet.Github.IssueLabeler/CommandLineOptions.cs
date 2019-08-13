// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;

namespace Microsoft.DotNet.Github.IssueLabeler
{
    internal class CommandLineOptions
    {
        [Option('e', "EndIndex", Required = true, HelpText = "Ending Github issue number")]
        public int EndIndex { get; set; }

        [Option('f', "OutputFile", Required = true, HelpText = ".tsv Output File Path")]
        public string Output { get; set; }

        [Option('o', "RepoOwner", Required = true, HelpText = "Repository owner")]
        public string Owner { get; set; }

        [Option('r', "RepoName", Required = true, HelpText = "Repository Name")]
        public string Repository { get; set; }

        [Option('s', "StartIndex", Required = true, HelpText = "Starting Github issue number")]
        public int StartIndex { get; set; }

        [Option('t', "GithubToken", Required = true, HelpText = "Github Access Token")]
        public string GithubToken { get; set; }
    }
}
