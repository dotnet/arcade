// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace Microsoft.DotNet.DarcLib
{
    public class GitFileManager
    {
        private readonly IGitRepo _gitClient;
        private readonly ILogger _logger;

        public static HashSet<string> DependencyFiles
        {
            get
            {
                return new HashSet<string>()
                {
                    VersionFilePath.VersionDetailsXml,
                    VersionFilePath.VersionProps,
                    VersionFilePath.GlobalJson
                };
            }
        }

        public GitFileManager(IGitRepo gitRepo, ILogger logger)
        {
            _gitClient = gitRepo;
            _logger = logger;
        }

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
            _logger.LogInformation($"Reading '{VersionFilePath.GlobalJson}' in repo '{repoUri}' and branch '{branch}'...");

            string fileContent = await _gitClient.GetFileContentsAsync(VersionFilePath.GlobalJson, repoUri, branch);

            JObject jsonContent = JObject.Parse(fileContent);

            return jsonContent;
        }

        public async Task<IEnumerable<DependencyDetail>> ParseVersionDetailsXmlAsync(string repoUri, string branch)
        {
            _logger.LogInformation($"Getting a collection of BuildAsset objects from '{VersionFilePath.VersionDetailsXml}' in repo '{repoUri}' and branch '{branch}'...");

            List<DependencyDetail> BuildAssets = new List<DependencyDetail>();
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
                            if (dependency.NodeType != XmlNodeType.Comment && dependency.NodeType != XmlNodeType.Whitespace)
                            {
                                DependencyDetail BuildAsset = new DependencyDetail
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
                _logger.LogError($"There was an error while reading '{VersionFilePath.VersionDetailsXml}' and it came back empty. Look for exceptions above.");

                return BuildAssets;
            }

            _logger.LogInformation($"Getting a collection of BuildAsset objects from '{VersionFilePath.VersionDetailsXml}' in repo '{repoUri}' and branch '{branch}' succeeded!");

            return BuildAssets;
        }

        public async Task<GitFileContentContainer> UpdateDependencyFiles(IEnumerable<DependencyDetail> itemsToUpdate, string repoUri, string branch)
        {
            XmlDocument versionDetails = await ReadVersionDetailsXmlAsync(repoUri, branch);
            XmlDocument versionProps = await ReadVersionPropsAsync(repoUri, branch);
            JObject globalJson = await ReadGlobalJsonAsync(repoUri, branch);

            foreach (DependencyDetail itemToUpdate in itemsToUpdate)
            {
                XmlNodeList versionList = versionDetails.SelectNodes($"//Dependency[@Name='{itemToUpdate.Name}']");

                if (versionList.Count != 1)
                {
                    if (versionList.Count == 0)
                    {
                        _logger.LogError($"No dependencies named '{itemToUpdate.Name}' found.");
                    }
                    else
                    {
                        _logger.LogError("The use of the same asset, even with a different version, is currently not supported.");
                    }

                    return null;
                }

                XmlNode nodeToUpdate = versionDetails.DocumentElement.SelectSingleNode($"//Dependency[@Name='{itemToUpdate.Name}']");
                nodeToUpdate.Attributes["Version"].Value = itemToUpdate.Version;
                nodeToUpdate.SelectSingleNode("Sha").InnerText = itemToUpdate.Commit;
                UpdateVersionFiles(versionProps, globalJson, itemToUpdate);
            }

            GitFileContentContainer fileContainer = new GitFileContentContainer
            {
                GlobalJson = new GitFile(VersionFilePath.GlobalJson, globalJson),
                VersionDetailsXml = new GitFile(VersionFilePath.VersionDetailsXml, versionDetails),
                VersionProps = new GitFile(VersionFilePath.VersionProps, versionProps)
            };

            return fileContainer;
        }

        private async Task<XmlDocument> ReadXmlFileAsync(string filePath, string repoUri, string branch)
        {
            _logger.LogInformation($"Reading '{filePath}' in repo '{repoUri}' and branch '{branch}'...");

            string fileContent = await _gitClient.GetFileContentsAsync(filePath, repoUri, branch);
            XmlDocument document = new XmlDocument();

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
            string namespaceName = "ns";
            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(versionProps.NameTable);
            namespaceManager.AddNamespace(namespaceName, versionProps.DocumentElement.Attributes["xmlns"].Value);

            string versionedName = itemToUpdate.Name.Replace(".", string.Empty);

            XmlNode versionNode = versionProps.DocumentElement.SelectSingleNode($"//{namespaceName}:{versionedName}Version", namespaceManager);

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
                else
                {
                    UpdateVersionGlobalJson(itemToUpdate, property.Value);
                }
            }
        }
    }
}
