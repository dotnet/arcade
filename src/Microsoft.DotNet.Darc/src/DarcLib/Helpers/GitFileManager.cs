// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.DarcLib
{
    public class GitFileManager
    {
        private readonly IGitRepo _gitClient;
        private readonly ILogger _logger;

        public GitFileManager(IGitRepo gitRepo, ILogger logger)
        {
            _gitClient = gitRepo;
            _logger = logger;
        }

        public static HashSet<string> DependencyFiles =>
            new HashSet<string>
            {
                VersionFiles.VersionDetailsXml,
                VersionFiles.VersionProps,
                VersionFiles.GlobalJson
            };

        public async Task<XmlDocument> ReadVersionDetailsXmlAsync(string repoUri, string branch)
        {
            XmlDocument document = await ReadXmlFileAsync(VersionFiles.VersionDetailsXml, repoUri, branch);
            return document;
        }

        public async Task<XmlDocument> ReadVersionPropsAsync(string repoUri, string branch)
        {
            XmlDocument document = await ReadXmlFileAsync(VersionFiles.VersionProps, repoUri, branch);
            return document;
        }

        public async Task<JObject> ReadGlobalJsonAsync(string repoUri, string branch)
        {
            _logger.LogInformation(
                $"Reading '{VersionFiles.GlobalJson}' in repo '{repoUri}' and branch '{branch}'...");

            string fileContent = await _gitClient.GetFileContentsAsync(VersionFiles.GlobalJson, repoUri, branch);

            JObject jsonContent = JObject.Parse(fileContent);

            return jsonContent;
        }

        public async Task<IEnumerable<DependencyDetail>> ParseVersionDetailsXmlAsync(string repoUri, string branch)
        {
            _logger.LogInformation(
                $"Getting a collection of BuildAsset objects from '{VersionFiles.VersionDetailsXml}' in repo '{repoUri}' and branch '{branch}'...");

            var BuildAssets = new List<DependencyDetail>();
            XmlDocument document = await ReadVersionDetailsXmlAsync(repoUri, branch);

            if (document != null)
            {
                BuildDependencies(document.DocumentElement.SelectNodes("//Dependency"));

                void BuildDependencies(XmlNodeList dependencies)
                {
                    if (dependencies.Count > 0)
                    {
                        foreach (XmlNode dependency in dependencies)
                        {
                            if (dependency.NodeType != XmlNodeType.Comment &&
                                dependency.NodeType != XmlNodeType.Whitespace)
                            {
                                var BuildAsset = new DependencyDetail
                                {
                                    Branch = branch,
                                    Name = dependency.Attributes["Name"].Value,
                                    RepoUri = dependency.SelectSingleNode("Uri").InnerText,
                                    Commit = dependency.SelectSingleNode("Sha").InnerText,
                                    Version = dependency.Attributes["Version"].Value
                                };

                                BuildAssets.Add(BuildAsset);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No dependencies defined in file.");
                    }
                }
            }
            else
            {
                _logger.LogError(
                    $"There was an error while reading '{VersionFiles.VersionDetailsXml}' and it came back empty. Look for exceptions above.");

                return BuildAssets;
            }

            _logger.LogInformation(
                $"Getting a collection of BuildAsset objects from '{VersionFiles.VersionDetailsXml}' in repo '{repoUri}' and branch '{branch}' succeeded!");

            return BuildAssets;
        }

        public async Task<GitFileContentContainer> UpdateDependencyFiles(
            IEnumerable<DependencyDetail> itemsToUpdate,
            string repoUri,
            string branch)
        {
            XmlDocument versionDetails = await ReadVersionDetailsXmlAsync(repoUri, branch);
            XmlDocument versionProps = await ReadVersionPropsAsync(repoUri, branch);
            JObject globalJson = await ReadGlobalJsonAsync(repoUri, branch);

            foreach (DependencyDetail itemToUpdate in itemsToUpdate)
            {
                // Use a case-insensitive update.
                XmlNodeList versionList = versionDetails.SelectNodes($"//Dependency[translate(@Name,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='{itemToUpdate.Name.ToLower()}']");

                if (versionList.Count != 1)
                {
                    if (versionList.Count == 0)
                    {
                        throw new DarcException($"No dependencies named '{itemToUpdate.Name}' found.");
                    }
                    else
                    {
                        throw new DarcException("The use of the same asset, even with a different version, is currently not supported.");
                    }
                }

                XmlNode nodeToUpdate = versionList.Item(0);
                nodeToUpdate.Attributes["Version"].Value = itemToUpdate.Version;
                nodeToUpdate.Attributes["Name"].Value = itemToUpdate.Name;
                nodeToUpdate.SelectSingleNode("Sha").InnerText = itemToUpdate.Commit;
                nodeToUpdate.SelectSingleNode("Uri").InnerText = itemToUpdate.RepoUri;
                UpdateVersionFiles(versionProps, globalJson, itemToUpdate);
            }

            var fileContainer = new GitFileContentContainer
            {
                GlobalJson = new GitFile(VersionFiles.GlobalJson, globalJson),
                VersionDetailsXml = new GitFile(VersionFiles.VersionDetailsXml, versionDetails),
                VersionProps = new GitFile(VersionFiles.VersionProps, versionProps)
            };

            return fileContainer;
        }

        public async Task AddDependencyToVersionDetails(
            string filePath,
            DependencyDetail dependency,
            DependencyType dependencyType)
        {
            XmlDocument versionDetails = await ReadVersionDetailsXmlAsync(filePath, null);

            XmlNode newDependency = versionDetails.CreateElement("Dependency");

            XmlAttribute nameAttribute = versionDetails.CreateAttribute("Name");
            nameAttribute.Value = dependency.Name;
            newDependency.Attributes.Append(nameAttribute);

            XmlAttribute versionAttribute = versionDetails.CreateAttribute("Version");
            versionAttribute.Value = dependency.Version;
            newDependency.Attributes.Append(versionAttribute);

            XmlNode uri = versionDetails.CreateElement("Uri");
            uri.InnerText = dependency.RepoUri;
            newDependency.AppendChild(uri);

            XmlNode sha = versionDetails.CreateElement("Sha");
            sha.InnerText = dependency.Commit;
            newDependency.AppendChild(sha);

            XmlNode dependencies = versionDetails.SelectSingleNode($"//{dependencyType}Dependencies");
            dependencies.AppendChild(newDependency);

            var file = new GitFile(filePath, versionDetails);
            File.WriteAllText(file.FilePath, file.Content);

            _logger.LogInformation(
                $"Dependency '{dependency.Name}' with version '{dependency.Version}' successfully added to Version.Details.xml");
        }

        public async Task AddDependencyToVersionProps(string filePath, DependencyDetail dependency)
        {
            XmlDocument versionProps = await ReadVersionPropsAsync(filePath, null);

            string versionedName = VersionFiles.CalculateVersionPropsElementName(dependency.Name);
            XmlNode versionNode = versionProps.DocumentElement.SelectNodes("//PropertyGroup").Item(0);

            XmlNode newDependency = versionProps.CreateElement(versionedName);
            newDependency.InnerText = dependency.Version;
            versionNode.AppendChild(newDependency);

            var file = new GitFile(filePath, versionProps);
            File.WriteAllText(file.FilePath, file.Content);

            _logger.LogInformation(
                $"Dependency '{dependency.Name}' with version '{dependency.Version}' successfully added to Version.props");
        }

        public async Task AddDependencyToGlobalJson(
            string filePath,
            string parentField,
            string dependencyName,
            string version)
        {
            JToken versionProperty = new JProperty(dependencyName, version);
            JObject globalJson = await ReadGlobalJsonAsync(filePath, null);
            JToken parent = globalJson[parentField];

            if (parent != null)
            {
                parent.Last.AddAfterSelf(versionProperty);
            }
            else
            {
                globalJson.Add(new JProperty(parentField, new JObject(versionProperty)));
            }

            var file = new GitFile(filePath, globalJson);
            File.WriteAllText(file.FilePath, file.Content);

            _logger.LogInformation(
                $"Dependency '{dependencyName}' with version '{version}' successfully added to global.json");
        }

        private async Task<XmlDocument> ReadXmlFileAsync(string filePath, string repoUri, string branch)
        {
            _logger.LogInformation($"Reading '{filePath}' in repo '{repoUri}' and branch '{branch}'...");

            string fileContent = await _gitClient.GetFileContentsAsync(filePath, repoUri, branch);
            var document = new XmlDocument();

            try
            {
                document.PreserveWhitespace = true;
                document.LoadXml(fileContent);
            }
            catch (Exception exc)
            {
                _logger.LogError($"There was an exception while loading '{filePath}'. Exception: {exc}");
                throw;
            }

            _logger.LogInformation($"Reading '{filePath}' from repo '{repoUri}' and branch '{branch}' succeeded!");

            return document;
        }

        private void UpdateVersionFiles(XmlDocument versionProps, JToken token, DependencyDetail itemToUpdate)
        {
            string versionElementName = VersionFiles.CalculateVersionPropsElementName(itemToUpdate.Name);

            XmlNode versionNode = versionProps.DocumentElement.SelectSingleNode($"//{versionElementName}");

            if (versionNode != null)
            {
                versionNode.InnerText = itemToUpdate.Version;
            }
            else
            {
                UpdateVersionGlobalJson(itemToUpdate, token);
            }
        }

        private void UpdateVersionGlobalJson(DependencyDetail itemToUpdate, JToken token)
        {
            string versionElementName = VersionFiles.CalculateGlobalJsonElementName(itemToUpdate.Name);

            foreach (JProperty property in token.Children<JProperty>())
            {
                if (property.Name == versionElementName)
                {
                    property.Value = new JValue(itemToUpdate.Version);
                    break;
                }

                UpdateVersionGlobalJson(itemToUpdate, property.Value);
            }
        }

        /// <summary>
        ///     Verify the local repository has correct and consistent dependency information.
        ///     Currently, this implementation checks:
        ///     - global.json, Version.props and Version.Details.xml can be parsed.
        ///     - There are no duplicated dependencies in Version.Details.xml
        ///     - If a dependency exists in Version.Details.xml and in version.props/global.json, the versions match.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="branch"></param>
        /// <returns>Async task</returns>
        public async Task<bool> Verify(string repo, string branch)
        {
            Task<IEnumerable<DependencyDetail>> dependencyDetails;
            Task<XmlDocument> versionProps;
            Task<JObject> globalJson;

            try
            {
                dependencyDetails = ParseVersionDetailsXmlAsync(repo, branch);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to parse {VersionFiles.VersionDetailsXml}");
                return false;
            }

            try
            {
                versionProps = ReadVersionPropsAsync(repo, branch);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to read {VersionFiles.VersionProps}");
                return false;
            }

            try
            {
                globalJson = ReadGlobalJsonAsync(repo, branch);
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
        ///     Verify that any dependency that exists in global.json and Version.Details.xml (e.g. Arcade SDK) 
        ///     has matching version numbers.
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
