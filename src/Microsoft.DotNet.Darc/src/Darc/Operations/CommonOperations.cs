// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    public class CommonOperations
    {
        public static DependencyGraphNode BuildFirstLevelGraphFromLocal(DependencyDetail dependency, ILogger logger, Local localClient, string remotesMap = null, string reposFolder = null)
        {
            string repoPath = null;

            if (!string.IsNullOrEmpty(remotesMap))
            {
                if (string.IsNullOrEmpty(dependency.RepoUri))
                {
                    throw new ArgumentException($"When setting --remotes-map a uri has to be included in Version.Details.xml for asset '{dependency.Name}'...");
                }

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
                    // If a repo folder or a mapping was not set we use the current parent parent folder.
                    string gitDir = LocalCommands.GetGitDir(logger);
                    string parent = Directory.GetParent(gitDir).FullName;
                    folder = Directory.GetParent(parent).FullName;
                }

                if (string.IsNullOrEmpty(dependency.Commit))
                {
                    throw new ArgumentException($"When setting --repos-folder a commit has to be included in Version.Details.xml for asset '{dependency.Name}'...");
                }

                repoPath = LocalCommands.GetRepoPathFromFolder(folder, dependency.Commit, logger);

                if (string.IsNullOrEmpty(repoPath))
                {
                    throw new Exception($"Commit '{dependency.Commit}' was not found in any folder in '{folder}'. Make sure a folder for '{dependency.RepoUri}' exists "
                        + "and it has all the latest changes...");
                }
            }

            Exception exception = null;
            DependencyGraphNode node = null;

            try
            {
                string fileContents = LocalCommands.Show(repoPath, dependency.Commit, VersionFilePath.VersionDetailsXml, logger);
                IEnumerable<DependencyDetail> dependencies = localClient.GetDependenciesFromFileContents(fileContents);
                node = dependency.ToGraphNode(dependencies);
            }
            catch (Exception exc)
            {
                exception = exc;
            }

            if (exception != null)
            {
                throw exception;
            }

            return node;
        }

        public static async Task<DependencyGraphNode> BuildFirstLevelGraphFromRemoteAsync(DependencyDetail dependency, DarcSettings darcSettings, ILogger logger)
        {
            DependencyGraphNode node = null;
            Remote remote = new Remote(darcSettings, logger);
            IEnumerable<DependencyDetail> dependencies = await remote.GetDependenciesAsync(dependency.RepoUri, dependency.Commit);
            node = dependency.ToGraphNode(dependencies);
            return node;
        }
    }
}
