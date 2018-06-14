// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;

namespace Microsoft.DotNet.GitSync.CommitManager
{
    internal class CommandLineOptions
    {
        [Option('k', Required = true, HelpText = "Azure Account Key")]
        public string Key { get; set; }

        [Option('u', Required = true, HelpText = "Azure Account Name")]
        public string Username { get; set; }

        [Option('r', Required = true, HelpText = "Repo to which commit was made")]
        public string Repository { get; set; }

        [Option('b', Required = true, HelpText = "Branch to Mirror")]
        public string Branch { get; set; }

        [Option('c', Required = true, HelpText = "Sha of the Commit")]
        public string Commit { get; set; }

        [Option('m', Required = true, HelpText = "Commit Message")]
        public string Message { get; set; }

        [Option('a', Default = "dotnet-bot", HelpText = "Author")]
        public string Author { get; set; }
    }
}
