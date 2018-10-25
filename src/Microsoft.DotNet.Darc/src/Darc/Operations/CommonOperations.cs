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
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    public class CommonOperations
    {
        public static async Task<DependencyGraph> GetDependencyGraphAsync(
            DependencyDetail dependency,
            bool remote,
            ILogger logger,
            string reposFolder = null,
            string remotesMap = null)
        {
            HashSet<DependencyGraphNode> flatGraph = new HashSet<DependencyGraphNode>();
            DependencyGraphNode graphNode = new DependencyGraphNode(dependency);
            Stack<DependencyGraphNode> graph = new Stack<DependencyGraphNode>();
            graph.Push(graphNode);
            flatGraph.Add(graphNode);

            while (graph.Count > 0)
            {
                DependencyGraphNode node = graph.Pop();
                IEnumerable<DependencyDetail> dependencies = null;

                if (remote)
                {
                    DarcSettings darcSettings = LocalSettings.GetDarcSettings(new GetDependencyGraphCommandLineOptions(), logger, node.DependencyDetail.RepoUri);
                    Remote remoteClient = new Remote(darcSettings, logger);
                    dependencies = await remoteClient.GetDependenciesAsync(node.DependencyDetail.RepoUri, node.DependencyDetail.Commit);
                }
                else
                {
                    string repoPath = GetRepoPath(node.DependencyDetail, remotesMap, reposFolder, logger);

                    if (!string.IsNullOrEmpty(repoPath))
                    {
                        Local local = new Local($"{repoPath}/.git", logger);
                        string fileContents = LocalHelpers.Show(repoPath, node.DependencyDetail.Commit, VersionFiles.VersionDetailsXml, logger);
                        dependencies = local.GetDependenciesFromFileContents(fileContents);
                    }
                }

                if (dependencies != null)
                {
                    foreach (DependencyDetail dependencyDetail in dependencies)
                    {
                        DependencyGraphNode dependencyGraphNode = new DependencyGraphNode(dependencyDetail, node.ParentStack);
                        dependencyGraphNode.ParentStack.Add(node.DependencyDetail.RepoUri);

                        if (!graphNode.ParentStack.Contains(dependencyDetail.RepoUri) && dependencyDetail.RepoUri != node.DependencyDetail.RepoUri)
                        {
                            node.ChildNodes.Add(dependencyGraphNode);
                            graph.Push(dependencyGraphNode);
                            flatGraph.Add(dependencyGraphNode);
                        }
                    }
                }
            }

            return new DependencyGraph(graphNode, flatGraph);
        }

        private static string GetRepoPath(DependencyDetail dependency, string remotesMap, string reposFolder, ILogger logger)
        {
            string repoPath = null;

            if (!string.IsNullOrEmpty(remotesMap))
            {
                string[] keyValuePairs = remotesMap.Split(';');

                foreach (string keyValue in keyValuePairs)
                {
                    string[] kv = keyValue.Split(',');

                    if (kv[0] == dependency.RepoUri)
                    {
                        repoPath = kv[1];
                        break;
                    }
                }

                if (string.IsNullOrEmpty(repoPath))
                {
                    throw new Exception($"A key matching '{dependency.RepoUri}' was not found in the mapping. Please make sure to include it...");
                }
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
                    // If a repo folder or a mapping was not set we use the current parent's parent folder.
                    string gitDir = LocalHelpers.GetGitDir(logger);
                    string parent = Directory.GetParent(gitDir).FullName;
                    folder = Directory.GetParent(parent).FullName;
                }

                // There are cases when the sha is not specified in Version.Details.xml since owners want that Maestro++ fills this in. Without a sha
                // we cannot walk the graph. We do not fail the process but display/return a dependency with no sha and for that graph path
                // that would be the end of the walk
                if (string.IsNullOrEmpty(dependency.Commit))
                {
                    return null;
                }

                repoPath = LocalHelpers.GetRepoPathFromFolder(folder, dependency.Commit, logger);

                if (string.IsNullOrEmpty(repoPath))
                {
                    throw new Exception($"Commit '{dependency.Commit}' was not found in any folder in '{folder}'. Make sure a folder for '{dependency.RepoUri}' exists "
                        + "and it has all the latest changes...");
                }
            }

            return repoPath;
        }
    }
}
