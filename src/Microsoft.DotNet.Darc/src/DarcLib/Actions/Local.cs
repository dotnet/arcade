// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    public class Local : ILocal
    {
        private readonly GitFileManager _fileManager;
        private readonly IGitRepo _gitClient;
        private readonly ILogger _logger;
        // TODO: Make these not constants and instead attempt to give more accurate information commit, branch, repo name, etc.)
        private string _repo;
        private const string _branch = "current";

        public Local(string gitPath, ILogger logger)
        {
            _repo = Directory.GetParent(gitPath).FullName;
            _logger = logger;
            _gitClient = new LocalGitClient(gitPath, _logger);
            _fileManager = new GitFileManager(_gitClient, _logger);
        }

        /// <summary>
        /// Retrieves all dependencies from the local repo
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<DependencyDetail>> GetDependencies()
        {
            return await _fileManager.ParseVersionDetailsXmlAsync(_repo, _branch);
        }

        /// <summary>
        /// Adds a dependency to the dependency files
        /// </summary>
        /// <returns></returns>
        public async Task<int> AddDependencies(DependencyDetail dependency, DependencyType dependencyType)
        {
            if (DependencyOperations.IsWellKnownDependency(dependency.Name))
            {
                //json
                await _fileManager.AddDependencyToVersionDetails(Path.Combine(_repo, VersionFilePath.VersionDetailsXml), dependency, DependencyType.Toolset);
            }
            else
            {
                await _fileManager.AddDependencyToVersionProps(Path.Combine(_repo, VersionFilePath.VersionProps), dependency);
                await _fileManager.AddDependencyToVersionDetails(Path.Combine(_repo, VersionFilePath.VersionDetailsXml), dependency, dependencyType);
            }

            return 0;
        }
    }
}
