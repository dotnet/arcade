// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
            _gitClient = new LocalGitClient(gitPath, _logger);
            _fileManager = new GitFileManager(_gitClient, _logger);
        }

        /// <summary>
        ///     Adds a dependency to the dependency files
        /// </summary>
        /// <returns></returns>
        public async Task AddDependenciesAsync(DependencyDetail dependency, DependencyType dependencyType)
        {
            if (GetDependenciesAsync(dependency.Name).GetAwaiter().GetResult().Any())
            {
                throw new DependencyException($"Dependency {dependency.Name} already exists in this repository");
            }

            if (DependencyOperations.TryGetKnownUpdater(dependency.Name, out Delegate function))
            {
                await (Task) function.DynamicInvoke(_fileManager, _repo, dependency);
            }
            else
            {
                await _fileManager.AddDependencyToVersionProps(
                    Path.Combine(_repo, VersionFilePath.VersionProps),
                    dependency);
                await _fileManager.AddDependencyToVersionDetails(
                    Path.Combine(_repo, VersionFilePath.VersionDetailsXml),
                    dependency,
                    dependencyType);
            }
        }

        /// <summary>
        ///     Gets the local dependencies
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<DependencyDetail>> GetDependenciesAsync()
        {
            return (await _fileManager.ParseVersionDetailsXmlAsync(Path.Combine(_repo, VersionFilePath.VersionDetailsXml), null)).Where(
                dependency => string.IsNullOrEmpty(name) || dependency.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
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
