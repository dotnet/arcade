// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
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
                VersionFilePath.VersionDetailsXml,
                VersionFilePath.VersionProps,
                VersionFilePath.GlobalJson
            };

        public async Task<XmlDocument> ReadVersionDetailsXmlAsync(string repoUri, string branch)
        {
            XmlDocument document = await ReadXmlFileAsync(VersionFilePath.VersionDetailsXml, repoUri, branch);
            return document;
        }

        public async Task<XmlDocument> ReadVersionPropsAsync(string repoUri, string branch)
        {
            XmlDocument document = await ReadXmlFileAsync(VersionFilePath.VersionProps, repoUri, branch);
            return document;
        }

        public async Task<JObject> ReadGlobalJsonAsync(string repoUri, string branch)
        {
            _logger.LogInformation(
                $"Reading '{VersionFilePath.GlobalJson}' in repo '{repoUri}' and branch '{branch}'...");

            string fileContent = await _gitClient.GetFileContentsAsync(VersionFilePath.GlobalJson, repoUri, branch);

            JObject jsonContent = JObject.Parse(fileContent);

            return jsonContent;
        }

        public async Task<IEnumerable<DependencyDetail>> ParseVersionDetailsXmlAsync(string repoUri, string branch)
        {
            _logger.LogInformation(
                $"Getting a collection of BuildAsset objects from '{VersionFilePath.VersionDetailsXml}' in repo '{repoUri}' and branch '{branch}'...");

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
                    $"There was an error while reading '{VersionFilePath.VersionDetailsXml}' and it came back empty. Look for exceptions above.");

                return BuildAssets;
            }

            _logger.LogInformation(
                $"Getting a collection of BuildAsset objects from '{VersionFilePath.VersionDetailsXml}' in repo '{repoUri}' and branch '{branch}' succeeded!");

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
                GlobalJson = new GitFile(VersionFilePath.GlobalJson, globalJson),
                VersionDetailsXml = new GitFile(VersionFilePath.VersionDetailsXml, versionDetails),
                VersionProps = new GitFile(VersionFilePath.VersionProps, versionProps)
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

            string versionedName = dependency.Name.Replace(".", string.Empty);
            versionedName = $"{versionedName}Version";

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
            string versionedName = itemToUpdate.Name.Replace(".", string.Empty);

            XmlNode versionNode = versionProps.DocumentElement.SelectSingleNode($"//{versionedName}Version");

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
            foreach (JProperty property in token.Children<JProperty>())
            {
                if (property.Name == itemToUpdate.Name)
                {
                    property.Value = new JValue(itemToUpdate.Version);
                    break;
                }

                UpdateVersionGlobalJson(itemToUpdate, property.Value);
            }
        }
    }
}
