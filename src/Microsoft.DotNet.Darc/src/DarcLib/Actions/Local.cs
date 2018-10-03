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
        /// Adds a dependency to the dependency files
        /// </summary>
        /// <returns></returns>
        public async Task AddDependencies(DependencyDetail dependency, DependencyType dependencyType)
        {
            if (DependencyOperations.TryGetKnownUpdater(dependency.Name, out Delegate function))
            {
                await (Task)function.DynamicInvoke(_fileManager, _repo, dependency);
            }
            else
            {
                await _fileManager.AddDependencyToVersionProps(Path.Combine(_repo, VersionFilePath.VersionProps), dependency);
                await _fileManager.AddDependencyToVersionDetails(Path.Combine(_repo, VersionFilePath.VersionDetailsXml), dependency, dependencyType);
            }
        }

        /// <summary>
        /// Gets the local dependencies
        /// </summary>
        /// <returns></returns>
        public async Task GetDependencies(string name)
        {
            IEnumerable<DependencyDetail> dependencies = await _fileManager.ParseVersionDetailsXmlAsync(Path.Combine(_repo, VersionFilePath.VersionDetailsXml), null);

            if (!string.IsNullOrEmpty(name))
            {
                DependencyDetail dependency = dependencies.Where(d => d.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

                if (dependency == null)
                {
                    throw new Exception($"A dependency with name '{name}' was not found...");
                }

                LogDependency(dependency);
            }

            foreach (DependencyDetail dependency in dependencies)
            {
                LogDependency(dependency);

                Console.WriteLine();
            }
        }

        private void LogDependency(DependencyDetail dependency)
        {
            Console.WriteLine($"Name:    {dependency.Name}");
            Console.WriteLine($"Version: {dependency.Version}");
            Console.WriteLine($"Repo:    {dependency.RepoUri}");
            Console.WriteLine($"Commit:  {dependency.Commit}");
        }
    }
}
