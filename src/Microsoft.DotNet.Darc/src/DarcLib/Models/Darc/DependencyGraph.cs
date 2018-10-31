// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DarcLib.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    public class DependencyGraph
    {
        private static Dictionary<string, string> _remotesMapping = null;

        public DependencyGraph(
            DependencyGraphNode graph, 
            HashSet<DependencyDetail> flatGraph)
        {
            Graph = graph;
            FlatGraph = flatGraph;
        }

        public DependencyGraphNode Graph { get; set; }

        public HashSet<DependencyDetail> FlatGraph { get; set; }

        public static async Task<DependencyGraph> GetDependencyGraphAsync(
            DarcSettings darcSettings,
            DependencyDetail dependency,
            bool remote,
            ILogger logger,
            string reposFolder = null,
            IEnumerable<string> remotesMap = null,
            string testPath = null)
        {
            // Fail fast if darcSettings is null in a remote scenario
            if (remote && darcSettings == null)
            {
                throw new DarcException("In a remote scenario 'DarcSettings' have to be set.");
            }

            HashSet<DependencyDetail> uniqueDependencyDetails = new HashSet<DependencyDetail>(
                new DependencyDetailComparer());
            DependencyGraphNode graphNode = new DependencyGraphNode(dependency);
            Stack<DependencyGraphNode> nodesToVisit = new Stack<DependencyGraphNode>();

            nodesToVisit.Push(graphNode);
            uniqueDependencyDetails.Add(graphNode.DependencyDetail);

            while (nodesToVisit.Count > 0)
            {
                DependencyGraphNode node = nodesToVisit.Pop();
                IEnumerable<DependencyDetail> dependencies = await GetDependenciesAsync(
                    darcSettings, 
                    remote, 
                    logger, 
                    node, 
                    remotesMap, 
                    reposFolder, 
                    testPath);

                if (dependencies != null)
                {
                    foreach (DependencyDetail dependencyDetail in dependencies)
                    {
                        DependencyGraphNode dependencyGraphNode = new DependencyGraphNode(
                            dependencyDetail, 
                            node.VisitedNodes);
                        dependencyGraphNode.VisitedNodes.Add(node.DependencyDetail.RepoUri);

                        if (!graphNode.VisitedNodes.Contains(dependencyDetail.RepoUri) && 
                            dependencyDetail.RepoUri != node.DependencyDetail.RepoUri)
                        {
                            node.ChildNodes.Add(dependencyGraphNode);
                            nodesToVisit.Push(dependencyGraphNode);
                            uniqueDependencyDetails.Add(dependencyGraphNode.DependencyDetail);
                        }
                    }
                }
            }

            return new DependencyGraph(graphNode, uniqueDependencyDetails);
        }

        private static string GetRepoPath(
            DependencyDetail dependency, 
            IEnumerable<string> remotesMap, 
            string reposFolder, 
            ILogger logger)
        {
            string repoPath = null;

            if (remotesMap != null)
            {
                if (_remotesMapping == null)
                {
                    _remotesMapping = CreateRemotesMapping(remotesMap);
                }

                if (!_remotesMapping.ContainsKey(repoPath))
                {
                    throw new DarcException($"A key matching '{dependency.RepoUri}' was not " +
                        $"found in the mapping. Please make sure to include it...");
                }

                repoPath = _remotesMapping[repoPath];
            }
            else
            {
                string folder = null;

                if (!string.IsNullOrEmpty(reposFolder))
                {
                    folder = reposFolder;
                }
                else
                {
                    // If a repo folder or a mapping was not set we use the current parent's 
                    // parent folder.
                    string gitDir = LocalHelpers.GetGitDir(logger);
                    string parent = Directory.GetParent(gitDir).FullName;
                    folder = Directory.GetParent(parent).FullName;
                }

                // There are cases when the sha is not specified in Version.Details.xml 
                // since owners want that Maestro++ fills this in. Without a sha we 
                // cannot walk the graph. We do not fail the process but display/return 
                // a dependency with no sha and for that graph path that would be the end of the walk
                if (string.IsNullOrEmpty(dependency.Commit))
                {
                    return null;
                }

                repoPath = LocalHelpers.GetRepoPathFromFolder(folder, dependency.Commit, logger);

                if (string.IsNullOrEmpty(repoPath))
                {
                    throw new DarcException($"Commit '{dependency.Commit}' was not found in any " +
                        $"folder in '{folder}'. Make sure a folder for '{dependency.RepoUri}' exists " +
                        $"and it has all the latest changes...");
                }
            }

            return repoPath;
        }

        private static async Task<IEnumerable<DependencyDetail>> GetDependenciesAsync(
            DarcSettings darcSettings, 
            bool remote, 
            ILogger logger, 
            DependencyGraphNode node, 
            IEnumerable<string> remotesMap, 
            string reposFolder, 
            string testPath = null)
        {
            try
            {
                IEnumerable<DependencyDetail> dependencies = null;

                if (!string.IsNullOrEmpty(testPath))
                {
                    testPath = Path.Combine(
                                testPath,
                                node.DependencyDetail.RepoUri,
                                node.DependencyDetail.Commit);

                    if (!string.IsNullOrEmpty(node.DependencyDetail.Commit) && 
                        Directory.Exists(testPath))
                    {
                        Local local = new Local(
                            Path.Combine(
                                testPath,
                                ".git"),
                            logger);
                        dependencies = await local.GetDependenciesAsync();
                    }
                }
                else if (remote)
                {
                    Remote remoteClient = new Remote(darcSettings, logger);
                    dependencies = await remoteClient.GetDependenciesAsync(
                        node.DependencyDetail.RepoUri, 
                        node.DependencyDetail.Commit);
                }
                else
                {
                    string repoPath = GetRepoPath(node.DependencyDetail, remotesMap, reposFolder, logger);

                    if (!string.IsNullOrEmpty(repoPath))
                    {
                        // Local's constructor expects the repo's .git folder to be passed in. In this 
                        // particular case we could pass any folder under 'repoPath' or even a fake one 
                        // but we use .git to keep things consistent to what Local expects
                        Local local = new Local($"{repoPath}/.git", logger);
                        string fileContents = LocalHelpers.GitShow(
                            repoPath,
                            node.DependencyDetail.Commit,
                            VersionFiles.VersionDetailsXml,
                            logger);
                        dependencies = local.GetDependenciesFromFileContents(fileContents);
                    }
                }

                return dependencies;
            }
            catch (Exception exc)
            {
                logger.LogError(exc, $"Something failed while trying the fetch the " +
                    $"dependencies of repo '{node.DependencyDetail.RepoUri}' at sha " +
                    $"'{node.DependencyDetail.Commit}'");
                throw;
            }
        }

        private static Dictionary<string, string> CreateRemotesMapping(IEnumerable<string> remotesMap)
        {
            Dictionary<string, string> remotesMapping = new Dictionary<string, string>();

            foreach (string remotes in remotesMap)
            {
                string[] keyValuePairs = remotes.Split(';');

                foreach (string keyValue in keyValuePairs)
                {
                    string[] kv = keyValue.Split(',');
                    remotesMapping.Add(kv[0], kv[1]);
                }
            }

            return remotesMapping;
        }
    }
}
