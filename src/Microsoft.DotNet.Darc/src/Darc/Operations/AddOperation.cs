// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;
using System;

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

        public override int Execute()
        {
            DependencyType type = _options.Type.ToLower() == "toolset" ? DependencyType.Toolset : DependencyType.Product;

            Local local = new Local(LocalCommands.GetGitDir(Logger), Logger);

            DependencyDetail dependency = new DependencyDetail
            {
                Name = _options.Name,
                Version = _options.Version,
                RepoUri = _options.RepoUri,
                Commit = _options.Commit
            };

            try
            {
                return local.AddDependencies(dependency, type).Result;
            }
            catch (Exception exc)
            {
                Logger.LogError($"Something failed while adding dependency '{dependency.Name}' {dependency.Version}. Exception: {exc.Message}");
                return Constants.ErrorCode;
            }
        }
    }
}
