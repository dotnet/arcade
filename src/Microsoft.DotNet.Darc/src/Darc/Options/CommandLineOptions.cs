// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;

namespace Microsoft.DotNet.Darc.Options
{
    abstract class CommandLineOptions
    {
        [Option('p', "password", HelpText = "GitHub PAT used to authenticate against Maestro/BAR REST APIs if necessary.")]
        public string Password { get; set; }

        [Option('t', "token", HelpText = "Token used to authenticate VSTS or GitHub if necessary.")]
        public string Token { get; set; }

        [Option("verbose", HelpText = "Turn on verbose output")]
        public bool Verbose { get; set; }

        [Option("debug", HelpText = "Turn on debug output")]
        public bool Debug { get; set; }

        public string LocalDirectory { get { return System.IO.Directory.GetCurrentDirectory(); } }
    }
}
