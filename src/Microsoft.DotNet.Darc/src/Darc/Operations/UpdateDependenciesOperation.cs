// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.DarcLib.Helpers;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging;
using NuGet.Packaging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    class UpdateDependenciesOperation : Operation
    {
        UpdateDependenciesCommandLineOptions _options;
        public UpdateDependenciesOperation(UpdateDependenciesCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        /// <summary>
        /// Update local dependencies based on a specific channel.
        /// </summary>
        /// <param name="options">Command line options</param>
        /// <returns>Process exit code.</returns>
        public override async Task<int> ExecuteAsync()
        {
            try
            {
                DarcSettings darcSettings = LocalSettings.GetDarcSettings(_options, Logger);
                // TODO: PAT only used for pulling the arcade eng/common dir,
                // so hardcoded to GitHub PAT right now. Must be more generic in the future.
                darcSettings.GitType = GitRepoType.GitHub;
                LocalSettings localSettings = LocalSettings.LoadSettingsFile();
                darcSettings.PersonalAccessToken = !string.IsNullOrEmpty(localSettings.GitHubToken) ?
                                                    localSettings.GitHubToken :
                                                    _options.GitHubPat;

                Remote remote = new Remote(darcSettings, Logger);
                Local local = new Local(LocalHelpers.GetGitDir(Logger), Logger);
                List<DependencyDetail> dependenciesToUpdate = new List<DependencyDetail>();
                bool someUpToDate = false;
                string finalMessage = $"Local dependencies updated from channel '{_options.Channel}'.";

                // First we need to figure out what to query for.  Load Version.Details.xml and
                // find all repository uris, optionally restricted by the input dependency parameter.
                IEnumerable<DependencyDetail> dependencies = await local.GetDependenciesAsync(_options.Name);

                if (!dependencies.Any())
                {
                    Console.WriteLine("Found no dependencies to update.");
                    return Constants.ErrorCode;
                }

                if (!string.IsNullOrEmpty(_options.Name) && !string.IsNullOrEmpty(_options.Version))
                {
                    DependencyDetail dependency = dependencies.First();
                    dependency.Version = _options.Version;
                    dependenciesToUpdate.Add(dependency);

                    Console.WriteLine($"Updating '{dependency.Name}': '{dependency.Version}' => '{_options.Version}'");

                    finalMessage = $"Local dependency {_options.Name} updated to version '{_options.Version}'.";
                }
                else if (!string.IsNullOrEmpty(_options.PackagesFolder))
                {
                    try
                    {
                        dependenciesToUpdate.AddRange(GetDependenciesFromPackagesFolder(_options.PackagesFolder, dependencies));
                    }
                    catch (DarcException exc)
                    {
                        Logger.LogError(exc, $"Error: Failed to update dependencies based on folder '{_options.PackagesFolder}'");
                        return Constants.ErrorCode;
                    }

                    finalMessage = $"Local dependencies updated based on packages folder {_options.PackagesFolder}.";
                }
                else
                {
                    // Start channel query.
                    var channel = remote.GetChannelAsync(_options.Channel);

                    // Limit the number of BAR queries by grabbing the repo URIs and making a hash set.
                    var repositoryUrisForQuery = dependencies.Select(dependency => dependency.RepoUri).ToHashSet();
                    ConcurrentDictionary<string, Task<Build>> getLatestBuildTaskDictionary = new ConcurrentDictionary<string, Task<Build>>();

                    Channel channelInfo = await channel;
                    if (channelInfo == null)
                    {
                        Console.WriteLine($"Could not find a channel named '{_options.Channel}'.");
                        return Constants.ErrorCode;
                    }

                    foreach (string repoToQuery in repositoryUrisForQuery)
                    {
                        var latestBuild = remote.GetLatestBuildAsync(repoToQuery, channelInfo.Id.Value);
                        getLatestBuildTaskDictionary.TryAdd(repoToQuery, latestBuild);
                    }

                    // Now walk dependencies again and attempt the update
                    foreach (DependencyDetail dependency in dependencies)
                    {
                        Build build;
                        try
                        {
                            build = await getLatestBuildTaskDictionary[dependency.RepoUri];
                        }
                        catch (ApiErrorException e) when (e.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            Logger.LogTrace($"No build of '{dependency.RepoUri}' found on channel '{_options.Channel}'.");
                            continue;
                        }

                        Asset buildAsset = build.Assets.Where(asset => asset.Name.Equals(dependency.Name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                        if (buildAsset == null)
                        {
                            Logger.LogTrace($"Dependency '{dependency.Name}' not found in latest build of '{dependency.RepoUri}' on '{_options.Channel}', skipping.");
                            continue;
                        }

                        if (buildAsset.Version == dependency.Version &&
                            buildAsset.Name == dependency.Name &&
                            build.Repository == dependency.RepoUri &&
                            build.Commit == dependency.Commit)
                        {
                            // No changes
                            someUpToDate = true;
                            continue;
                        }

                        DependencyDetail updatedDependency = new DependencyDetail
                        {
                            // TODO: Not needed, but not currently provided in Build info. Will be available on next rollout.
                            Branch = null,
                            Commit = build.Commit,
                            // If casing changes, ensure that the dependency name gets updated.
                            Name = buildAsset.Name,
                            RepoUri = build.Repository,
                            Version = buildAsset.Version
                        };

                        dependenciesToUpdate.Add(updatedDependency);

                        // Print out what we are going to do.
                        Console.WriteLine($"Updating '{dependency.Name}': '{dependency.Version}' => '{updatedDependency.Version}'" +
                            $" (from build '{build.BuildNumber}' of '{build.Repository}')");
                        // Notify on casing changes.
                        if (buildAsset.Name != dependency.Name)
                        {
                            Console.WriteLine($"  Dependency name normalized to '{updatedDependency.Name}'");
                        }

                        dependenciesToUpdate.Add(updatedDependency);
                    }
                }

                if (!dependenciesToUpdate.Any())
                {
                    // If we found some dependencies already up to date,
                    // then we consider this a success. Otherwise, we didn't even
                    // find matching dependencies so we should let the user know.
                    if (someUpToDate)
                    {
                        Console.WriteLine($"All dependencies are up to date.");
                        return Constants.SuccessCode;
                    }
                    else
                    {
                        Console.WriteLine($"Found no dependencies to update.");
                        return Constants.ErrorCode;
                    }
                }

                if (_options.DryRun)
                {
                    return Constants.SuccessCode;
                }

                // Now call the local updater to run the update
                await local.UpdateDependenciesAsync(dependenciesToUpdate, remote);

                
                Console.WriteLine(finalMessage);

                return Constants.SuccessCode;
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Error: Failed to update dependencies to channel {_options.Channel}");
                return Constants.ErrorCode;
            }
        }

        private IEnumerable<DependencyDetail> GetDependenciesFromPackagesFolder(string pathToFolder, IEnumerable<DependencyDetail> dependencies)
        {
            Dictionary<string, string> dependencyVersionMap = new Dictionary<string, string>();

            // Not using Linq to make sure there are no duplicates
            foreach (DependencyDetail dependency in dependencies)
            {
                if (!dependencyVersionMap.ContainsKey(dependency.Name))
                {
                    dependencyVersionMap.Add(dependency.Name, dependency.Version);
                }
            }

            List<DependencyDetail> updatedDependencies = new List<DependencyDetail>();

            if (!Directory.Exists(pathToFolder))
            {
                throw new DarcException($"Packages folder '{pathToFolder}' does not exist.");
            }

            IEnumerable<string> packages = Directory.GetFiles(pathToFolder, "*.nupkg");

            foreach (string package in packages)
            {
                ManifestMetadata manifestMetedata = PackagesHelper.GetManifestMetadata(package);

                if (dependencyVersionMap.ContainsKey(manifestMetedata.Id))
                {
                    string oldVersion = dependencyVersionMap[manifestMetedata.Id];

                    Console.WriteLine($"Updating '{manifestMetedata.Id}': '{oldVersion}' => '{manifestMetedata.Version.OriginalVersion}'");

                    updatedDependencies.Add(new DependencyDetail
                    {
                        Commit = manifestMetedata.Repository.Commit,
                        Name = manifestMetedata.Id,
                        RepoUri = manifestMetedata.Repository.Url,
                        Version = manifestMetedata.Version.OriginalVersion
                    });
                }
            }

            return updatedDependencies;
        }
    }
}
