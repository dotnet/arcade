// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    internal class GetOperation : Operation
    {
        GetCommandLineOptions _options;
        public GetOperation(GetCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        /// <summary>
        /// Implements the 'get' verb
        /// </summary>
        /// <param name="options"></param>
        public override async Task<int> ExecuteAsync()
        {
            Local local = new Local(LocalCommands.GetGitDir(Logger), Logger);

            var allDependencies = await local.GetDependencies();

            foreach (var dependency in allDependencies)
            {
                Console.WriteLine($"{dependency.Name} {dependency.Version} from {dependency.RepoUri}@{dependency.Commit}");
            }

            return Constants.SuccessCode;
        }
    }
}
