// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("get-dependencies", HelpText = "Get local dependencies.")]
    internal class GetDependenciesCommandLineOptions : CommandLineOptions
    {
        [Option('n', "name", HelpText = "Name of dependency to query for.")]
        public string Name { get; set; }

        public override Operation GetOperation()
        {
            return new GetDependenciesOperation(this);
        }
    }
}
