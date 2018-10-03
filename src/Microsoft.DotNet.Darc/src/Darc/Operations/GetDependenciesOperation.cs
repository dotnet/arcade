// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    internal class GetDependenciesOperation : Operation
    {
        private GetDependenciesCommandLineOptions _options;

        public GetDependenciesOperation(GetDependenciesCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        public override async Task<int> ExecuteAsync()
        {
            Local local = new Local(LocalCommands.GetGitDir(Logger), Logger);

            try
            {
                await local.GetDependencies(_options.Name);
                return Constants.SuccessCode;
            }
            catch (Exception exc)
            {
                if (!string.IsNullOrEmpty(_options.Name))
                {
                    Logger.LogError(exc, $"Something failed while querying for local dependency '{_options.Name}'.");
                }
                else
                {
                    Logger.LogError(exc, "Something failed while querying for local dependencies.");
                }
                
                return Constants.ErrorCode;
            }
        }
    }
}
