using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Microsoft.DotNet.Darc
{
    public class DependencyFileManager
    {
        private readonly GitHubClient gitHubClient;
        private const string VersionDetailsXmlPath = "eng/version.details.xml";
        private const string VersionPropsPath = "eng/Versions.props";
        private const string GlobalJsonPath = "global.json";
        private const string VersionPropsExpression = "VersionProps";

        public DependencyFileManager(string accessToken)
        {
            gitHubClient = new GitHubClient(accessToken);
        }

        public async Task<XmlDocument> ReadVersionDetailsXmlAsync(string repoUri, string branch)
        {
            XmlDocument document = await ReadXmlFileAsync(VersionDetailsXmlPath, repoUri, branch);
            return document;
        }

        public async Task<XmlDocument> ReadVersionPropsAsync(string repoUri, string branch)
        {
            XmlDocument document = await ReadXmlFileAsync(VersionPropsPath, repoUri, branch);
            return document;
        }

        public async Task<JObject> ReadGlobalJsonAsync(string repoUri, string branch)
        {
            Console.WriteLine($"Reading '{GlobalJsonPath}' in repo '{repoUri}' and branch '{branch}'...");

            string fileContent = await gitHubClient.GetFileContentsAsync(GlobalJsonPath, repoUri, branch);
            JObject jsonContent = JObject.Parse(fileContent);
            //var dict = jsonContent.Children()
            //               .OfType<JProperty>()
            //               .ToDictionary(x => x.Name, x => x.Value);

            //int c = (int)dict["CONTRATE"];
            return jsonContent;
        }

        public async Task<IEnumerable<DependencyItem>> ParseVersionDetailsXmlAsync(string repoUri, string branch)
        {
            Console.WriteLine($"Getting a collection of DependencyItem objects from '{VersionDetailsXmlPath}' in repo '{repoUri}' and branch '{branch}'...");

            List<DependencyItem> dependencyItems = new List<DependencyItem>();
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
                            DependencyItem dependencyItem = new DependencyItem
                            {
                                Branch = branch,
                                Name = childNode.Attributes["Name"].Value,
                                RepoUri = childNode.SelectSingleNode("Uri").InnerText,
                                Sha = childNode.SelectSingleNode("Sha").InnerText,
                                Version = childNode.Attributes["Version"].Value,
                                Type = dependencyType
                            };

                            dependencyItems.Add(dependencyItem);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No '{dependencyType}' defined in file.");
                    }
                }
            }
            else
            {
                Console.WriteLine($"There was an error while reading '{VersionDetailsXmlPath}' and it came back empty. Look for exceptions above.");
                return dependencyItems;
            }

            Console.WriteLine($"Getting a collection of DependencyItem objects from '{VersionDetailsXmlPath}' in repo '{repoUri}' and branch '{branch}' succeeded!");

            return dependencyItems;
        }

        public async Task<DependencyFileContentContainer> UpdateDependencyFiles(IEnumerable<DependencyItem> itemsToUpdate, string repoUri, string branch)
        {
            XmlDocument versionDetails = await ReadVersionDetailsXmlAsync(repoUri, branch);
            XmlDocument versionProps = await ReadVersionPropsAsync(repoUri, branch);
            JObject globalJson = await ReadGlobalJsonAsync(repoUri, branch);

            foreach (DependencyItem itemToUpdate in itemsToUpdate)
            {
                XmlNodeList versionList = versionDetails.SelectNodes($"//Dependency[@Name='{itemToUpdate.Name}']");
                
                if (versionList.Count == 0 || versionList.Count > 1)
                {
                    if (versionList.Count == 0)
                    {
                        Console.WriteLine($"No dependencies named {itemToUpdate.Name} found.");
                    }
                    else
                    {
                        Console.WriteLine(@"The use of the same asset even though it has a different version is currently not supported.");
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
                        XmlNode versionNode = versionProps.DocumentElement.SelectSingleNode($"{itemToUpdate.Name}Version");

                        if (versionNode != null)
                        {
                            versionNode.InnerText = itemToUpdate.Version;
                        }
                        else
                        {
                            Console.WriteLine($"{itemToUpdate.Name}Version not found in '{VersionPropsPath}'.");
                        }
                    }
                    else
                    {
                        if (dependencyToUpdate.SelectSingleNode("Expression") != null && dependencyToUpdate.SelectSingleNode("Expression").InnerText == VersionPropsExpression)
                        {
                            XmlNode versionNode = versionProps.DocumentElement.SelectSingleNode($"{itemToUpdate.Name}Version");

                            if (versionNode != null)
                            {
                                versionNode.InnerText = itemToUpdate.Version;
                            }
                            else
                            {
                                Console.WriteLine($"{itemToUpdate.Name}Version not found in '{VersionPropsPath}'.");
                            }
                        }
                        else
                        {
                            TryUpdateVersionInGlobalJson(itemToUpdate.Name, itemToUpdate.Version, globalJson);
                        }
                    }
                }
            }

            return new DependencyFileContentContainer();
        }

        private async Task<XmlDocument> ReadXmlFileAsync(string filePath, string repoUri, string branch)
        {
            Console.WriteLine($"Reading '{filePath}' in repo '{repoUri}' and branch '{branch}'...");

            string fileContent = await gitHubClient.GetFileContentsAsync(filePath, repoUri, branch);
            XmlDocument document = new XmlDocument();

            try
            {
                document.LoadXml(fileContent);
            }
            catch (Exception exc)
            {
                Console.WriteLine($"There was an exception while loading '{filePath}'. Exception: {exc}");
                return null;
            }

            Console.WriteLine($"Reading '{filePath}' from repo '{repoUri}' and branch '{branch}' succeeded!");

            return document;
        }

        private bool TryUpdateVersionInGlobalJson(string assetName, string version, JToken jsonContent)
        {
            return true;
        }
    }
}
