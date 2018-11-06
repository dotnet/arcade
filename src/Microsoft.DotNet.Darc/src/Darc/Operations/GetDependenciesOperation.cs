// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.DarcLib.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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
            Local local = new Local(LocalHelpers.GetGitDir(Logger), Logger);

            try
            {
                IEnumerable<DependencyDetail> dependencies = await local.GetDependenciesAsync(_options.Name);

                if (!string.IsNullOrEmpty(_options.Name))
                {
                    DependencyDetail dependency = dependencies.Where(d => d.Name.Equals(_options.Name, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

                    if (dependency == null)
                    {
                        throw new Exception($"A dependency with name '{_options.Name}' was not found...");
                    }

                    LogDependency(dependency);
                }

                foreach (DependencyDetail dependency in dependencies)
                {
                    LogDependency(dependency);

                    Console.WriteLine();
                }

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

        private void LogDependency(DependencyDetail dependency)
        {
            Console.WriteLine($"Name:    {dependency.Name}");
            Console.WriteLine($"Version: {dependency.Version}");
            Console.WriteLine($"Repo:    {dependency.RepoUri}");
            Console.WriteLine($"Commit:  {dependency.Commit}");
        }
    }
}
