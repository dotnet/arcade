// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("update-dependencies", HelpText = "Update local dependencies from a channel.")]
    class UpdateDependenciesCommandLineOptions : CommandLineOptions
    {
        [Option('c', "channel", HelpText = "Channel to pull dependencies from.")]
        public string Channel { get; set; }

        [Option('n', "name", HelpText = "Optional name of dependency to update.  Otherwise all " +
            "dependencies existing on 'channel' are updated.")]
        public string Name { get; set; }

        [Option('v', "version", HelpText = "The new version of dependency --name.")]
        public string Version { get; set; }

        [Option("packages-folder", HelpText = "An optional path to a folder which contains the NuGet " +
            "packages whose versions will be used to update existing dependencies.")]
        public string PackagesFolder { get; set; }

        [Option("dry-run", HelpText = "Show what will be updated, but make no changes.")]
        public bool DryRun { get; set; }

        public override Operation GetOperation()
        {
            return new UpdateDependenciesOperation(this);
        }
    }
}
