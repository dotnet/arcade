// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.DarcLib.Helpers;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
                darcSettings.PersonalAccessToken = localSettings.GitHubToken;

                Remote remote = new Remote(darcSettings, Logger);
                Local local = new Local(LocalHelpers.GetGitDir(Logger), Logger);

                // Start channel query.
                var channel = remote.GetChannelAsync(_options.Channel);

                // First we need to figure out what to query for.  Load Version.Details.xml and
                // find all repository uris, optionally restricted by the input dependency parameter.

                var dependencies = await local.GetDependenciesAsync(_options.Name);

                if (!dependencies.Any())
                {
                    Console.WriteLine("Found no dependencies to update.");
                    return Constants.ErrorCode;
                }

                // Limit the number of BAR queries by grabbing the repo URIs and making a hash set.
                var repositoryUrisForQuery = dependencies.Select(dependency => dependency.RepoUri).ToHashSet();
                ConcurrentDictionary<string, Task<Build>> buildDictionary = new ConcurrentDictionary<string, Task<Build>>();

                Channel channelInfo = await channel;
                if (channelInfo == null)
                {
                    Console.WriteLine($"Could not find a channel named '{_options.Channel}'.");
                    return Constants.ErrorCode;
                }

                foreach (string repoToQuery in repositoryUrisForQuery)
                {
                    var latestBuild = remote.GetLatestBuildAsync(repoToQuery, channelInfo.Id.Value);
                    buildDictionary.TryAdd(repoToQuery, latestBuild);
                }

                bool someUpToDate = false;
                List<DependencyDetail> dependenciesToUpdate = new List<DependencyDetail>();
                // Now walk dependencies again and attempt the update
                foreach (DependencyDetail dependency in dependencies)
                {
                    Build build = await buildDictionary[dependency.RepoUri];
                    if (build == null)
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
                        buildAsset.Name == dependency.Name)
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
                
                if (!dependenciesToUpdate.Any())
                {
                    // If we found some dependencies already up to date,
                    // then we consider this a success. Otherwise, we didn't even
                    // find matching dependencies so we should let the user know.
                    if (someUpToDate)
                    {
                        Console.WriteLine($"All dependencies from channel '{_options.Channel}' are up to date.");
                        return Constants.SuccessCode;
                    }
                    else
                    {
                        Console.WriteLine($"Found no dependencies to update on channel '{_options.Channel}'.");
                        return Constants.ErrorCode;
                    }
                }

                if (_options.DryRun)
                {
                    return Constants.SuccessCode;
                }

                // Now call the local updater to run the update
                await local.UpdateDependenciesAsync(dependenciesToUpdate, remote);

                Console.WriteLine($"Local dependencies updated from channel '{_options.Channel}'.");

                return Constants.SuccessCode;
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Error: Failed to update dependencies to channel {_options.Channel}");
                return Constants.ErrorCode;
            }
        }
    }
}
