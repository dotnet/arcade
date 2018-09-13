// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
            _repo = gitPath;
            _logger = logger;
            _gitClient = new LocalGitClient(gitPath);
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
    }
}
