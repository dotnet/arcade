using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace Microsoft.DotNet.Darc
{
    public class DependencyFileManager
    {
        private readonly IGitRepo _gitClient;
        private readonly ILogger _logger;
        private const string VersionPropsExpression = "VersionProps";
        private const string SdkVersionProperty = "version";

        public static HashSet<string> DependencyFiles
        {
            get
            {
                return new HashSet<string>()
                {
                    DependencyFilePath.VersionDetailsXml,
                    DependencyFilePath.VersionProps,
                    DependencyFilePath.GlobalJson
                };
            }
        }

        public DependencyFileManager(IGitRepo gitRepo, ILogger logger)
        {
            _gitClient = gitRepo;
            _logger = logger;
        }

        public async Task<XmlDocument> ReadVersionDetailsXmlAsync(string repoUri, string branch)
        {
            XmlDocument document = await ReadXmlFileAsync(DependencyFilePath.VersionDetailsXml, repoUri, branch);
            return document;
        }

        public async Task<XmlDocument> ReadVersionPropsAsync(string repoUri, string branch)
        {
            XmlDocument document = await ReadXmlFileAsync(DependencyFilePath.VersionProps, repoUri, branch);
            return document;
        }

        public async Task<JObject> ReadGlobalJsonAsync(string repoUri, string branch)
        {
            _logger.LogInformation($"Reading '{DependencyFilePath.GlobalJson}' in repo '{repoUri}' and branch '{branch}'...");

            string fileContent = await _gitClient.GetFileContentsAsync(DependencyFilePath.GlobalJson, repoUri, branch);

            JObject jsonContent = JObject.Parse(fileContent);

            return jsonContent;
        }

        public async Task<IEnumerable<BuildAsset>> ParseVersionDetailsXmlAsync(string repoUri, string branch)
        {
            _logger.LogInformation($"Getting a collection of BuildAsset objects from '{DependencyFilePath.VersionDetailsXml}' in repo '{repoUri}' and branch '{branch}'...");

            List<BuildAsset> BuildAssets = new List<BuildAsset>();
            XmlDocument document = await ReadVersionDetailsXmlAsync(repoUri, branch);

            if (document != null)
            {
                BuildDependencies(document.DocumentElement.SelectSingleNode("ProductDependencies"), DependencyType.Product);
                BuildDependencies(document.DocumentElement.SelectSingleNode("ToolsetDependencies"), DependencyType.Toolset);

                void BuildDependencies(XmlNode node, DependencyType dependencyType)
                {
                    if (node != null)
                    {
                        foreach (XmlNode childNode in node.ChildNodes)
                        {
                            if (childNode.NodeType != XmlNodeType.Comment && childNode.NodeType != XmlNodeType.Whitespace)
                            {
                                BuildAsset BuildAsset = new BuildAsset
                                {
                                    Branch = branch,
                                    Name = childNode.Attributes["Name"].Value,
                                    RepoUri = childNode.SelectSingleNode("Uri").InnerText,
                                    Sha = childNode.SelectSingleNode("Sha").InnerText,
                                    Version = childNode.Attributes["Version"].Value,
                                    Type = dependencyType
                                };

                                BuildAssets.Add(BuildAsset);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"No '{dependencyType}' defined in file.");
                    }
                }
            }
            else
            {
                _logger.LogError($"There was an error while reading '{DependencyFilePath.VersionDetailsXml}' and it came back empty. Look for exceptions above.");

                return BuildAssets;
            }

            _logger.LogInformation($"Getting a collection of BuildAsset objects from '{DependencyFilePath.VersionDetailsXml}' in repo '{repoUri}' and branch '{branch}' succeeded!");

            return BuildAssets;
        }

        public async Task<DependencyFileContentContainer> UpdateDependencyFiles(IEnumerable<BuildAsset> itemsToUpdate, string repoUri, string branch)
        {
            XmlDocument versionDetails = await ReadVersionDetailsXmlAsync(repoUri, branch);
            XmlDocument versionProps = await ReadVersionPropsAsync(repoUri, branch);
            JObject globalJson = await ReadGlobalJsonAsync(repoUri, branch);

            foreach (BuildAsset itemToUpdate in itemsToUpdate)
            {
                XmlNodeList versionList = versionDetails.SelectNodes($"//Dependency[@Name='{itemToUpdate.Name}']");

                if (versionList.Count == 0 || versionList.Count > 1)
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

                XmlNode parentNode = itemToUpdate.Type == DependencyType.Product
                    ? versionDetails.DocumentElement.SelectSingleNode("ProductDependencies")
                    : versionDetails.DocumentElement.SelectSingleNode("ToolsetDependencies");

                XmlNodeList nodesToUpdate = parentNode.SelectNodes($"//Dependency[@Name='{itemToUpdate.Name}']");

                foreach (XmlNode dependencyToUpdate in nodesToUpdate)
                {
                    dependencyToUpdate.Attributes["Version"].Value = itemToUpdate.Version;
                    dependencyToUpdate.SelectSingleNode("Sha").InnerText = itemToUpdate.Sha;

                    // If the dependency is of Product type we also have to update version.props
                    // If the dependency is of Toolset type and it has no <Expression> defined or not set to VersionProps we also update global.json
                    // if not, we update version.props
                    if (itemToUpdate.Type == DependencyType.Product)
                    {
                        UpdateVersionPropsDoc(versionProps, itemToUpdate);
                    }
                    else
                    {
                        if (dependencyToUpdate.SelectSingleNode("Expression") != null && dependencyToUpdate.SelectSingleNode("Expression").InnerText == VersionPropsExpression)
                        {
                            UpdateVersionPropsDoc(versionProps, itemToUpdate);
                        }
                        else
                        {
                            UpdateVersionGlobalJson(itemToUpdate.Name, itemToUpdate.Version, globalJson);
                        }
                    }
                }
            }

            DependencyFileContentContainer fileContainer = new DependencyFileContentContainer
            {
                GlobalJson = new DependencyFileContent(DependencyFilePath.GlobalJson, globalJson),
                VersionDetailsXml = new DependencyFileContent(DependencyFilePath.VersionDetailsXml, versionDetails),
                VersionProps = new DependencyFileContent(DependencyFilePath.VersionProps, versionProps)
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

        private void UpdateVersionPropsDoc(XmlDocument versionProps, BuildAsset itemToUpdate)
        {
            string namespaceName = "ns";
            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(versionProps.NameTable);
            namespaceManager.AddNamespace(namespaceName, versionProps.DocumentElement.Attributes["xmlns"].Value);

            XmlNode versionNode = versionProps.DocumentElement.SelectSingleNode($"//{namespaceName}:{itemToUpdate.Name}Version", namespaceManager);

            if (versionNode != null)
            {
                versionNode.InnerText = itemToUpdate.Version;
            }
            else
            {
                _logger.LogError($"'{itemToUpdate.Name}Version' not found in '{DependencyFilePath.VersionProps}'.");
            }
        }

        private void UpdateVersionGlobalJson(string assetName, string version, JToken token)
        {
            foreach (JProperty child in token.Children<JProperty>())
            {
                if (child.Name == assetName)
                {
                    if (child.HasValues && child.Value.ToString().IndexOf(SdkVersionProperty, StringComparison.CurrentCultureIgnoreCase) > 0)
                    {
                        UpdateVersionGlobalJson(SdkVersionProperty, version, child.Value);
                    }
                    else
                    {
                        child.Value = new JValue(version);
                    }

                    break;
                }
                else
                {
                    UpdateVersionGlobalJson(assetName, version, child.Value);
                }
            }
        }
    }
}
