// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

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
                    Path.Combine(_repo, VersionFiles.VersionProps),
                    dependency);
                await _fileManager.AddDependencyToVersionDetails(
                    Path.Combine(_repo, VersionFiles.VersionDetailsXml),
                    dependency,
                    dependencyType);
            }
        }

        /// <summary>
        ///     Gets the local dependencies
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<DependencyDetail>> GetDependenciesAsync(string name)
        {
            return (await _fileManager.ParseVersionDetailsXmlAsync(Path.Combine(_repo, VersionFiles.VersionDetailsXml), null)).Where(
                dependency => string.IsNullOrEmpty(name) || dependency.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     Verify the local repository has correct and consistent dependency information.
        ///     Currently, this implementation checks:
        ///     - global.json, Version.props and Version.Details.xml can be parsed.
        ///     - There are no duplicated dependencies in Version.Details.xml
        ///     - If a dependency exists in Version.Details.xml and in version.props/global.json, the versions match.
        /// </summary>
        /// <returns>Async task</returns>
        public async Task<bool> Verify()
        {
            Task<IEnumerable<DependencyDetail>> dependencyDetails;
            Task<XmlDocument> versionProps;
            Task<JObject> globalJson;

            try
            {
                dependencyDetails = _fileManager.ParseVersionDetailsXmlAsync(Path.Combine(_repo, VersionFiles.VersionDetailsXml), null);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to parse {VersionFiles.VersionDetailsXml}");
                return false;
            }

            try
            {
                versionProps = _fileManager.ReadVersionPropsAsync(Path.Combine(_repo, VersionFiles.VersionProps), null);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to read {VersionFiles.VersionProps}");
                return false;
            }

            try
            {
                globalJson = _fileManager.ReadGlobalJsonAsync(Path.Combine(_repo, VersionFiles.GlobalJson), null);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to read {VersionFiles.GlobalJson}");
                return false;
            }

            List<Task<bool>> verificationTasks = new List<Task<bool>>()
            {
                VerifyNoDuplicatedDependencies(await dependencyDetails),
                VerifyMatchingVersionProps(await dependencyDetails, await versionProps, out Task<HashSet<string>> utilizedVersionPropsDependencies),
                VerifyMatchingGlobalJson(await dependencyDetails, await globalJson, out Task<HashSet<string>> utilizedGlobalJsonDependencies),
                VerifyUtilizedDependencies(await dependencyDetails, new List<HashSet<string>>{ await utilizedVersionPropsDependencies, await utilizedGlobalJsonDependencies})
            };

            var results = await Task.WhenAll<bool>(verificationTasks);
            return results.All(result => result);
        }

        /// <summary>
        ///     Ensure that the dependency structure only contains one of each named dependency.
        /// </summary>
        /// <param name="dependencies">Parsed dependencies in the repository.</param>
        /// <returns>True if there are no duplicated dependencies.</returns>
        private Task<bool> VerifyNoDuplicatedDependencies(IEnumerable<DependencyDetail> dependencies)
        {
            bool result = true;
            HashSet<string> dependenciesBitVector = new HashSet<string>();
            foreach (var dependency in dependencies)
            {
                if (dependenciesBitVector.Contains(dependency.Name, StringComparer.OrdinalIgnoreCase))
                {
                    _logger.LogError($"The dependency '{dependency.Name}' appears more than once in '{VersionFiles.VersionDetailsXml}'");
                    result = false;
                }
                dependenciesBitVector.Add(dependency.Name);
            }
            return Task.FromResult(result);
        }

        /// <summary>
        ///     Verify that any dependency that exists in both Version.props and Version.Details.xml has matching version numbers.
        /// </summary>
        /// <param name="dependencies">Parsed dependencies in the repository.</param>
        /// <param name="versionProps">Parsed version props file</param>
        /// <returns></returns>
        private Task<bool> VerifyMatchingVersionProps(IEnumerable<DependencyDetail> dependencies, XmlDocument versionProps, out Task<HashSet<string>> utilizedDependencies)
        {
            HashSet<string> utilizedSet = new HashSet<string>();
            bool result = true;
            foreach (var dependency in dependencies)
            {
                string versionedName = VersionFiles.CalculateVersionPropsElementName(dependency.Name);
                XmlNode versionNode = versionProps.DocumentElement.SelectSingleNode($"//{versionedName}");

                if (versionNode != null)
                {
                    // Validate that the casing matches for consistency
                    if (versionNode.Name != versionedName)
                    {
                        _logger.LogError($"The dependency '{dependency.Name}' has a case mismatch between '{VersionFiles.VersionProps}' and '{VersionFiles.VersionDetailsXml}' " +
                            $"('{versionNode.Name}' vs. '{versionedName}')");
                        result = false;
                    }
                    // Validate innner version matches
                    if (versionNode.InnerText != dependency.Version)
                    {
                        _logger.LogError($"The dependency '{dependency.Name}' has a version mismatch between '{VersionFiles.VersionProps}' and '{VersionFiles.VersionDetailsXml}' " +
                            $"('{versionNode.InnerText}' vs. '{dependency.Version}')");
                        result = false;
                    }
                    utilizedSet.Add(dependency.Name);
                }
            }
            utilizedDependencies = Task.FromResult(utilizedSet);
            return Task.FromResult(result);
        }

        /// <summary>
        ///     Verify that any dependency that exists in global.json and version.details xml (e.g. Arcade SDK) has matching version numbers.
        /// </summary>
        /// <param name="dependencies">Parsed dependencies in the repository.</param>
        /// <param name="rootToken">Root global.json token.</param>
        /// <returns></returns>
        private Task<bool> VerifyMatchingGlobalJson(IEnumerable<DependencyDetail> dependencies, JObject rootToken, out Task<HashSet<string>> utilizedDependencies)
        {
            HashSet<string> utilizedSet = new HashSet<string>();
            bool result = true;
            foreach (var dependency in dependencies)
            {
                string versionedName = VersionFiles.CalculateGlobalJsonElementName(dependency.Name);
                JToken dependencyNode = FindDependency(rootToken, versionedName);
                if (dependencyNode != null)
                {
                    // Should be a string with matching version.
                    if (dependencyNode.Type != JTokenType.Property || ((JProperty)dependencyNode).Value.Type != JTokenType.String)
                    {
                        _logger.LogError($"The element '{dependency.Name}' in '{VersionFiles.GlobalJson}' should be a property with a value of type string.");
                        result = false;
                        continue;
                    }
                    JProperty property = (JProperty)dependencyNode;
                    // Validate that the casing matches for consistency
                    if (property.Name != versionedName)
                    {
                        _logger.LogError($"The dependency '{dependency.Name}' has a case mismatch between '{VersionFiles.GlobalJson}' and '{VersionFiles.VersionDetailsXml}' " +
                            $"('{property.Name}' vs. '{versionedName}')");
                        result = false;
                    }
                    // Validate version
                    JToken value = (JToken)property.Value;
                    if (value.Value<string>() != dependency.Version)
                    {
                        _logger.LogError($"The dependency '{dependency.Name}' has a version mismatch between '{VersionFiles.GlobalJson}' and '{VersionFiles.VersionDetailsXml}' " +
                            $"('{value.Value<string>()}' vs. '{dependency.Version}')");
                    }

                    utilizedSet.Add(dependency.Name);
                }
            }
            utilizedDependencies = Task.FromResult(utilizedSet);
            return Task.FromResult(result);
        }

        /// <summary>
        ///     Recursively walks a json tree to find a property called <paramref name="elementName"/> in its children
        /// </summary>
        /// <param name="currentToken">Current token to walk.</param>
        /// <param name="elementName">Property name to find.</param>
        /// <returns>Token with name 'name' or null if it does not exist.</returns>
        private JToken FindDependency(JToken currentToken, string elementName)
        {
            foreach (JProperty property in currentToken.Children<JProperty>())
            {
                if (property.Name.Equals(elementName, StringComparison.OrdinalIgnoreCase))
                {
                    return property;
                }

                JToken foundToken = FindDependency(property.Value, elementName);
                if (foundToken != null)
                {
                    return foundToken;
                }
            }

            return null;
        }

        /// <summary>
        ///     Check that each dependency in <paramref name="dependencies"/> exists in at least one of the <paramref name="utilizedDependencySets"/>
        /// </summary>
        /// <param name="dependencies">Parsed dependencies in the repository.</param>
        /// <param name="utilizedDependencySets">Bit vectors dependency expression locations.</param>
        /// <returns></returns>
        private Task<bool> VerifyUtilizedDependencies(IEnumerable<DependencyDetail> dependencies, IEnumerable<HashSet<string>> utilizedDependencySets)
        {
            bool result = true;
            foreach (var dependency in dependencies)
            {
                if (!utilizedDependencySets.Where(set => set.Contains(dependency.Name)).Any())
                {
                    _logger.LogWarning($"The dependency '{dependency.Name}' is unused in either '{VersionFiles.GlobalJson}' or '{VersionFiles.VersionProps}'");
                    result = false;
                }
            }
            return Task.FromResult(result);
        }
    }
}
