// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    internal class GetDependenciesOperation : Operation
    {
        private readonly GetDependenciesCommandLineOptions _options;
        private readonly ConsoleLogger _consoleLogger;

        public GetDependenciesOperation(GetDependenciesCommandLineOptions options)
            : base(options)
        {
            _options = options;
            _consoleLogger = new ConsoleLogger();
        }

        public override async Task<int> ExecuteAsync()
        {
            Local local = new Local(LocalCommands.GetGitDir(Logger), Logger);

            List<DependencyGraphNode> graph = new List<DependencyGraphNode>();

            try
            {
                IEnumerable<DependencyDetail> dependencies = await local.GetDependenciesAsync(_options.Name);

                if (!string.IsNullOrEmpty(_options.Name))
                {
                    DependencyDetail dependency = dependencies.FirstOrDefault();

                    if (dependency == null)
                    {
                        throw new Exception($"A dependency with name '{_options.Name}' was not found...");
                    }

                    DependencyGraphNode node = await GetDependencyGraphNodeAsync(dependency, local);
                    graph.Add(node);
                }
                else
                {
                    graph = await GetDependencyGraph(dependencies, local);
                }

                _consoleLogger.LogDependencyGraph(graph, _options.Flat);

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

        private async Task<DependencyGraphNode> GetDependencyGraphNodeAsync(DependencyDetail dependency, Local localClient)
        {
            DependencyGraphNode node = dependency.ToGraphNode();

            if (_options.RepoSha)
            {
                if (_options.Local)
                {
                    node = CommonOperations.BuildFirstLevelGraphFromLocal(dependency, Logger, localClient, _options.RemotesMap, _options.ReposFolder);
                }
                else
                {
                    DarcSettings darcSettings = LocalCommands.GetSettings(_options, Logger, dependency.RepoUri);
                    node = await CommonOperations.BuildFirstLevelGraphFromRemoteAsync(dependency, darcSettings, Logger);
                }
            }

            return node;
        }

        private async Task<List<DependencyGraphNode>> GetDependencyGraph(IEnumerable<DependencyDetail> dependencies, Local localClient)
        {
            List<DependencyGraphNode> childNodes = new List<DependencyGraphNode>();

            foreach (DependencyDetail dependency in dependencies)
            {
                DependencyGraphNode node = await GetDependencyGraphNodeAsync(dependency, localClient);
                childNodes.Add(node);
            }

            return childNodes;
        }
    }
}
