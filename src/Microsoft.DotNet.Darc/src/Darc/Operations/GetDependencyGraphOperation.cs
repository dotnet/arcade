// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
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
    internal class GetDependencyGraphOperation : Operation
    {
        private GetDependencyGraphCommandLineOptions _options;
        private readonly HashSet<string> _flatList = new HashSet<string>();

        public GetDependencyGraphOperation(GetDependencyGraphCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        public override async Task<int> ExecuteAsync()
        {
            try
            {
                Local local = new Local(LocalHelpers.GetGitDir(Logger), Logger);
                IEnumerable<DependencyDetail> dependencies = await local.GetDependenciesAsync(
                    _options.AssetName);
                DarcSettings darcSettings = null;

                if (_options.Remote)
                {
                    if (string.IsNullOrEmpty(_options.RepoUri))
                    {
                        Logger.LogError("If '--remote' is set '--repo-uri' is required.");

                        return Constants.ErrorCode;
                    }

                    darcSettings = LocalSettings.GetDarcSettings(
                        _options, 
                        Logger, 
                        _options.RepoUri);
                    Remote remote = new Remote(darcSettings, Logger);
                    dependencies = await remote.GetDependenciesAsync(
                        _options.RepoUri, 
                        _options.Branch, 
                        _options.AssetName);
                }

                List<DependencyGraph> graph = await CreateGraphAsync(dependencies, darcSettings);

                if (graph == null)
                {
                    return Constants.ErrorCode;
                }

                if (_options.Flat)
                {
                    LogFlatDependencyGraph(graph);
                }
                else
                {
                    LogDependencyGraph(graph);
                }

                return Constants.SuccessCode;
            }
            catch (Exception exc)
            {
                Logger.LogError(exc, "Something failed while getting the dependency graph.");

                return Constants.ErrorCode;
            }
        }

        /// <summary>
        /// Create the graph or graphs dependending on the amount of base dependencies 
        /// passed in as an input.
        /// </summary>
        /// <param name="dependencies">Input dependencies.</param>
        /// <param name="darcSettings">The Darc settings.</param>
        /// <returns>Collection of graph nodes.</returns>
        private async Task<List<DependencyGraph>> CreateGraphAsync(
            IEnumerable<DependencyDetail> dependencies, 
            DarcSettings darcSettings)
        {
            List<DependencyGraph> graph = new List<DependencyGraph>();

            if (string.IsNullOrEmpty(_options.AssetName))
            {
                await Task.WhenAll(dependencies.Select(
                    dependency => AddNodeToGraphAsync(
                        darcSettings, 
                        dependency, 
                        graph)));
            }
            else
            {
                DependencyDetail dependency = dependencies.FirstOrDefault();

                if (dependency == null)
                {
                    Logger.LogError($"Dependency '{_options.AssetName}' was not found.");
                    return null;
                }

                await AddNodeToGraphAsync(darcSettings, dependency, graph);
            }

            return graph;
        }

        private async Task AddNodeToGraphAsync(
            DarcSettings darcSettings, 
            DependencyDetail dependency, 
            List<DependencyGraph> graph)
        {
            DependencyGraph dependencyGraph = await DependencyGraph.GetDependencyGraphAsync(
                darcSettings, 
                dependency, 
                _options.Remote, 
                Logger, 
                _options.ReposFolder, 
                _options.RemotesMap);
            graph.Add(dependencyGraph);
        }

        private void LogDependency(DependencyDetail dependency, string indent = "")
        {
            Console.WriteLine($"{indent}- Name:    {dependency.Name}");
            Console.WriteLine($"{indent}  Version: {dependency.Version}");
            Console.WriteLine($"{indent}  Repo:    {dependency.RepoUri}");
            Console.WriteLine($"{indent}  Commit:  {dependency.Commit}");
        }

        private void LogFlatDependencyGraph(List<DependencyGraph> flatGraphs)
        {
            foreach (DependencyGraph flatGraph in flatGraphs)
            {
                foreach (DependencyDetail graphNode in flatGraph.FlatGraph)
                {
                    Console.WriteLine($"- Repo:    {graphNode.RepoUri}");
                    Console.WriteLine($"  Commit:  {graphNode.Commit}");
                }

                Console.WriteLine();
            }
        }

        private void LogDependencyGraph(List<DependencyGraph> graph)
        {
            LogDependencyGraph(graph.Select(g => g.Graph));
        }

        private void LogDependencyGraph(IEnumerable<DependencyGraphNode> graph, string indent = "")
        {
            foreach (DependencyGraphNode node in graph)
            {
                LogDependency(node.DependencyDetail, indent);

                if (node.ChildNodes != null)
                {
                    LogDependencyGraph(node.ChildNodes, indent + "  ");
                }

                Console.WriteLine();
            }
        }
    }
}
