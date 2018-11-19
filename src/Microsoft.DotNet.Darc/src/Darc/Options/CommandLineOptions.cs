// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;

namespace Microsoft.DotNet.Darc.Options
{
    abstract class CommandLineOptions
    {
        [Option('p', "password", HelpText = "BAR password.")]
        public string BuildAssetRegistryPassword { get; set; }

        [Option("github-pat", HelpText = "Token used to authenticate GitHub.")]
        public string GitHubPat { get; set; }

        [Option("azdev-pat", HelpText = "Token used to authenticate to Azure DevOps.")]
        public string AzureDevOpsPat { get; set; }

        [Option("bar-uri", HelpText = "URI of the build asset registry service to use.")]
        public string BuildAssetRegistryBaseUri { get; set; }

        [Option("verbose", HelpText = "Turn on verbose output.")]
        public bool Verbose { get; set; }

        [Option("debug", HelpText = "Turn on debug output.")]
        public bool Debug { get; set; }

        public abstract Operation GetOperation();
    }
}
