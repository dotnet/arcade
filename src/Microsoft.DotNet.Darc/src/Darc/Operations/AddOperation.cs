// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.DarcLib.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    internal class AddOperation : Operation
    {
        AddCommandLineOptions _options;
        public AddOperation(AddCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        public override async Task<int> ExecuteAsync()
        {
            DependencyType type = _options.Type.ToLower() == "toolset" ? DependencyType.Toolset : DependencyType.Product;

            Local local = new Local(LocalHelpers.GetGitDir(Logger), Logger);

            DependencyDetail dependency = new DependencyDetail
            {
                Name = _options.Name,
                Version = _options.Version ?? string.Empty,
                RepoUri = _options.RepoUri ?? string.Empty,
                Commit = _options.Commit ?? string.Empty
            };

            try
            {
                await local.AddDependencyAsync(dependency, type);
                return Constants.SuccessCode;
            }
            catch (Exception exc)
            {
                Logger.LogError(exc, $"Failed to add dependency '{dependency.Name}' to repository.");
                return Constants.ErrorCode;
            }
        }
    }
}
