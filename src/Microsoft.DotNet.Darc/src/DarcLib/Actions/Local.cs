// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    public class Local : ILocal
    {
        private const string _branch = "current";
        private readonly GitFileManager _fileManager;
        private readonly IGitRepo _gitClient;

        private readonly ILogger _logger;

        // TODO: Make these not constants and instead attempt to give more accurate information commit, branch, repo name, etc.)
        private readonly string _repo;

        public Local(string gitPath, ILogger logger)
        {
            _repo = Directory.GetParent(gitPath).FullName;
            _logger = logger;
            _gitClient = new LocalGitClient(_logger);
            _fileManager = new GitFileManager(_gitClient, _logger);
        }

        /// <summary>
        ///     Adds a dependency to the dependency files
        /// </summary>
        /// <returns></returns>
        public async Task AddDependencyAsync(DependencyDetail dependency, DependencyType dependencyType)
        {
            // TODO: https://github.com/dotnet/arcade/issues/1095
            // This should be getting back a container and writing the files from here.
            await _fileManager.AddDependencyAsync(dependency, dependencyType, _repo, null);
        }

        /// <summary>
        ///     Updates existing dependencies in the dependency files
        /// </summary>
        /// <param name="dependencies">Dependencies that need updates.</param>
        /// <param name="remote">Remote instance for gathering eng/common script updates.</param>
        /// <returns></returns>
        public async Task UpdateDependenciesAsync(List<DependencyDetail> dependencies, IRemote remote)
        {
            // TODO: This should use known updaters, but today the updaters for global.json can only
            // add, not actually update.  This needs a fix. https://github.com/dotnet/arcade/issues/1095
            /*List<DependencyDetail> defaultUpdates = new List<DependencyDetail>(, IRemote remote);
            foreach (DependencyDetail dependency in dependencies)
            {
                if (DependencyOperations.TryGetKnownUpdater(dependency.Name, out Delegate function))
                {
                    await (Task)function.DynamicInvoke(_fileManager, _repo, dependency);
                }
                else
                {
                    defaultUpdates.Add(dependency);
                }
            }*/

            var fileContainer = await _fileManager.UpdateDependencyFiles(dependencies, _repo, null);
            List<GitFile> filesToUpdate = fileContainer.GetFilesToCommit();

            // TODO: This needs to be moved into some consistent handling between local/remote and add/update:
            // https://github.com/dotnet/arcade/issues/1095
            // If we are updating the arcade sdk we need to update the eng/common files as well
            DependencyDetail arcadeItem = dependencies.FirstOrDefault(
                i => string.Equals(i.Name, "Microsoft.DotNet.Arcade.Sdk", StringComparison.OrdinalIgnoreCase));

            if (arcadeItem != null)
            {
                try
                {
                    List<GitFile> engCommonFiles = await remote.GetCommonScriptFilesAsync(arcadeItem.RepoUri, arcadeItem.Commit);
                    filesToUpdate.AddRange(engCommonFiles);

                    List<GitFile> localEngCommonFiles = await _gitClient.GetFilesForCommitAsync(null, null, "eng/common");

                    foreach (GitFile file in localEngCommonFiles)
                    {
                        if (!engCommonFiles.Where(f => f.FilePath == file.FilePath).Any())
                        {
                            file.Operation = GitFileOperation.Delete;
                            filesToUpdate.Add(file);
                        }
                    }
                }
                catch (Exception exc) when 
                (exc.Message == "Not Found")
                {
                    _logger.LogWarning("Could not update 'eng/common'. Most likely this is a scenario " +
                        "where a packages folder was passed and the commit which generated them is not " +
                        "yet pushed.");
                }
            }

            // Push on local does not commit.
            await _gitClient.PushFilesAsync(filesToUpdate, _repo, null, null);
        }

        /// <summary>
        ///     Gets the local dependencies
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<DependencyDetail>> GetDependenciesAsync(string name = null)
        {
            return (await _fileManager.ParseVersionDetailsXmlAsync(_repo, null)).Where(
                dependency => string.IsNullOrEmpty(name) || dependency.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     Verify the local repository has correct and consistent dependency information
        /// </summary>
        /// <returns>True if verification succeeds, false otherwise.</returns>
        public Task<bool> Verify()
        {
            return _fileManager.Verify(_repo, null);
        }

        /// <summary>
        /// Gets local dependencies from a local repository
        /// </summary>
        /// <returns></returns>
        public IEnumerable<DependencyDetail> GetDependenciesFromFileContents(string fileContents)
        {
            return _fileManager.ParseVersionDetailsXml(fileContents);
        }
    }
}
