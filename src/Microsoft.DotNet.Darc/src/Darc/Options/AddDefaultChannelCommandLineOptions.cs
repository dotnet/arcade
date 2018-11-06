// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("add-default-channel", HelpText = "Add a channel that a build of a branch+repository is automatically applied to.")]
    internal class AddDefaultChannelCommandLineOptions : CommandLineOptions
    {
        [Option("channel", Required = true, HelpText = "Name of channel that a build of 'branch' and 'repo' should be applied to.")]
        public string Channel { get; set; }

        [Option("branch", Required = true, HelpText = "Build of 'repo' on this branch will be automatically applied to 'channel'")]
        public string Branch { get; set; }

        [Option("repo", Required = true, HelpText = "Build of this repo repo on 'branch' will be automatically applied to 'channel'")]
        public string Repository { get; set; }

        public override Operation GetOperation()
        {
            return new AddDefaultChannelOperation(this);
        }
    }
}
