// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("get", HelpText = "Query information about dependencies.")]
    internal class GetCommandLineOptions : CommandLineOptions
    {
        [Option('n', "name", Required = true, HelpText = "Name of dependency to query for.")]
        string Name { get; set; }
    }
}
