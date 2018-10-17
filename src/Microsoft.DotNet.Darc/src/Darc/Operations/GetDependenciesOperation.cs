// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    internal class GetDependenciesOperation : Operation
    {
        private readonly GetDependenciesCommandLineOptions _options;

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
                IEnumerable<DependencyDetail> dependencies = await local.GetDependenciesAsync();

                if (!string.IsNullOrEmpty(_options.Name))
                {
                    DependencyDetail dependency = dependencies.Where(d => d.Name.Equals(_options.Name, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

                    if (dependency == null)
                    {
                        throw new Exception($"A dependency with name '{_options.Name}' was not found...");
                    }

                    await LogDependencyAsync(dependency, _options.RepoSha, _options.Local, local);
                }
                else
                {
                    foreach (DependencyDetail dependency in dependencies)
                    {
                        await LogDependencyAsync(dependency, _options.RepoSha, _options.Local, local);
                    }
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

        private async Task LogDependencyAsync(DependencyDetail dependency, bool repoSha, bool local, Local localClient)
        {
            ConsoleLogger.LogDependency(dependency, _options.Flat);

            if (repoSha)
            {
                IEnumerable<DependencyDetail> dependenciesAtSha = null;

                if (local)
                {
                    string repoPath = null;

                    if (!string.IsNullOrEmpty(_options.RemotesMap))
                    {
                        if (string.IsNullOrEmpty(dependency.RepoUri))
                        {
                            throw new ArgumentException($"When setting --remotes-map a uri has to be included in Version.Details.xml for asset '{dependency.Name}'...");
                        }

                        string[] keyValuePairs = _options.RemotesMap.Split(';');

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

                        if (!string.IsNullOrEmpty(_options.ReposFolder))
                        {
                            folder = _options.ReposFolder;
                        }
                        else
                        {
                            // If a repo folder or a mapping was not set we use the current parent parent folder.
                            string gitDir = LocalCommands.GetGitDir(Logger);
                            string parent = Directory.GetParent(gitDir).FullName;
                            folder = Directory.GetParent(parent).FullName;
                        }

                        if (string.IsNullOrEmpty(dependency.Commit))
                        {
                            throw new ArgumentException($"When setting --repos-folder a commit has to be included in Version.Details.xml for asset '{dependency.Name}'...");
                        }

                        repoPath = LocalCommands.GetRepoPathFromFolder(folder, dependency.Commit, Logger);

                        if (string.IsNullOrEmpty(repoPath))
                        {
                            throw new Exception($"Commit '{dependency.Commit}' was not found in any folder in '{folder}'. Make sure a folder for '{dependency.RepoUri}' exists "
                                + "and it has all the latest changes...");
                        }
                    }

                    Exception exception = null;

                    try
                    {
                        string fileContents = LocalCommands.Show(repoPath, dependency.Commit, VersionFilePath.VersionDetailsXml, Logger);
                        dependenciesAtSha = localClient.GetDependenciesFromFileContents(fileContents);
                    }
                    catch (Exception exc)
                    {
                        exception = exc;
                    }

                    if (exception != null)
                    {
                        throw exception;
                    }
                }
                else
                {
                    DarcSettings darcSettings = LocalCommands.GetSettings(_options, Logger, dependency.RepoUri);
                    Remote remote = new Remote(darcSettings, Logger);
                    dependenciesAtSha = await remote.GetDependenciesAsync(dependency.RepoUri, dependency.Commit);
                }

                foreach (DependencyDetail dependencyAtSha in dependenciesAtSha)
                {
                    ConsoleLogger.LogDependency(dependencyAtSha, _options.Flat, "    ");
                }
            }
        }
    }
}
