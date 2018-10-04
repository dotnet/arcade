// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("get-dependencies", HelpText = "Get local dependencies.")]
    internal class GetDependenciesCommandLineOptions : CommandLineOptions
    {
        [Option('n', "name", HelpText = "Name of dependency to query for.")]
        public string Name { get; set; }

        [Option("repo-sha", HelpText = "Get the repo+sha which produced a specific dependency specified by --name or all dependencies in a repository.")]
        public bool RepoSha { get; set; }

        [Option('l', "local", HelpText = "Required is repo-sha is set and we want to get the repo+sha combinations from local repositories. False by default.")]
        public bool Local { get; set; }

        [Option("repos-folder", HelpText = @"Full path to folder where all the repos are locally stored. i.e. C:\repos")]
        public string ReposFolder { get; set; }

        [Option("remotes-map", HelpText = @"';' separated key value pair defining the remote to local path mapping. i.e 'https://github.com/dotnet/arcade,C:\repos\arcade;'"
            + @"https://github.com/dotnet/corefx,C:\repos\corefx.")]
        public string RemotesMap { get; set; }

        [Option('f', "flat", HelpText = @"Returns a unique set of repository+sha combination.")]
        public bool Flat { get; set; }
    }
}
