// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("delete-default-channel", HelpText = "Remove a default channel association.")]
    internal class DeleteDefaultChannelCommandLineOptions : CommandLineOptions
    {
        [Option("channel", HelpText = "Name of channel that builds of 'repository' and 'branch' should not apply to.")]
        public string Channel { get; set; }

        [Option("branch", Required = true, HelpText = "Repository that should have its default association removed.")]
        public string Branch { get; set; }

        [Option("repo", Required = true, HelpText = "Branch that should have its default association removed.")]
        public string Repository { get; set; }

        public override Operation GetOperation()
        {
            return new DeleteDefaultChannelOperation(this);
        }
    }
}
