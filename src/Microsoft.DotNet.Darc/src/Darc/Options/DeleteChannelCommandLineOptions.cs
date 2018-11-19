// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("delete-channel", HelpText = "Deletes an existing channel.")]
    internal class DeleteChannelCommandLineOptions : CommandLineOptions
    {
        [Option('n', "name", Required = true, HelpText = "Name of channel to delete.")]
        public string Name { get; set; }

        public override Operation GetOperation()
        {
            return new DeleteChannelOperation(this);
        }
    }
}
