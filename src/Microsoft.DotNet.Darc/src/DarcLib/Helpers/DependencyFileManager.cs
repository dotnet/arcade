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

        public DependencyFileManager(string accessToken)
        {
            gitHubClient = new GitHubClient(accessToken);
        }

        public async Task<IEnumerable<DependencyItem>> ReadVersionDetailsXmlAsync(string repoUri, string branch)
        {
            Console.WriteLine($"Getting a collection of DependencyItem objects from '{VersionDetailsXmlPath}' in repo '{repoUri}' and branch '{branch}'...");

            List<DependencyItem> dependencyItems = new List<DependencyItem>();
            string fileContent = await gitHubClient.GetFileContentsAsync(VersionDetailsXmlPath, repoUri, branch);
            XmlDocument document = new XmlDocument();

            try
            {
                document.LoadXml(fileContent);
                BuildDependencies(document.DocumentElement.SelectSingleNode("ProductDependencies"), DependencyType.Product);
                BuildDependencies(document.DocumentElement.SelectSingleNode("ToolsetDependencies"), DependencyType.Toolset);

                void BuildDependencies(XmlNode node, DependencyType dependencyType)
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
            }
            catch (Exception exc)
            {
                Console.WriteLine($"There was an exception while parsing '{VersionDetailsXmlPath}'. Exception: {exc}");
                return dependencyItems;
            }

            Console.WriteLine($"Getting a collection of DependencyItem objects from '{VersionDetailsXmlPath}' in repo '{repoUri}' and branch '{branch}' succeeded!");

            return dependencyItems;
        }
    }
}
