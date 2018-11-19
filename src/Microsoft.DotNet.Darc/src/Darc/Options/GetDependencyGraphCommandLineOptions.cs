// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;
using System.Collections.Generic;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("get-dependency-graph", HelpText = "Get local dependencies.")]
    internal class GetDependencyGraphCommandLineOptions : CommandLineOptions
    {
        [Option('r', "remote", HelpText = "Get the graph from remote repositories.")]
        public bool Remote { get; set; }

        [Option("repo-uri", HelpText = "If remote is set to true, we fetch Version.Details.xml from the remote 'repo-uri'.")]
        public string RepoUri { get; set; }

        [Option('b', "branch", HelpText = "If 'repo-uri' is set the branch is required.")]
        public string Branch { get; set; }

        [Option("asset-name", HelpText = "Get the graph based on a single asset and not the whole Version.Details.xml contents.")]
        public string AssetName { get; set; }

        [Option("repos-folder", HelpText = @"Full path to folder where all the repos are locally stored. i.e. C:\repos")]
        public string ReposFolder { get; set; }

        [Option("remotes-map", Separator = ';', HelpText = @"';' separated key value pair defining the remote to local path mapping. i.e 'https://github.com/dotnet/arcade,C:\repos\arcade;'"
           + @"https://github.com/dotnet/corefx,C:\repos\corefx.")]
        public IEnumerable<string> RemotesMap { get; set; }

        [Option('f', "flat", HelpText = @"Returns a unique set of repository+sha combination.")]
        public bool Flat { get; set; }

        public override Operation GetOperation()
        {
            return new GetDependencyGraphOperation(this);
        }
    }
}
