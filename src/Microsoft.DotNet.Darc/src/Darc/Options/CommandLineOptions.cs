// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;

namespace Microsoft.DotNet.Darc.Options
{
    abstract class CommandLineOptions
    {
        [Option("verbose", HelpText = "Turn on verbose output")]
        public bool Verbose { get; set; }

        [Option("debug", HelpText = "Turn on debug output")]
        public bool Debug { get; set; }
    }
}
