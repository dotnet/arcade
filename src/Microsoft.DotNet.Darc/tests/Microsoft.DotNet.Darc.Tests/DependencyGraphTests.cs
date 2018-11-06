// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DarcLib;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Xunit;

namespace Microsoft.DotNet.Darc.Tests
{
    public class DependencyGraphTests
    {
        private const string DefaultOutputFileName = "output.xml";

        [Fact]
        public void ValidateFullGraph()
        {
            DependencyDetail dependencyDetail = new DependencyDetail
            {
                Name = "Base.Asset",
                RepoUri = "RepoA",
                Commit = "shaA",
                Version = "1.0.0"
            };

            DependencyTestDriver.GetGraphAndCompare("DependencyGraph", async driver =>
            {
                return await driver.GetDependencyGraph(dependencyDetail);
            },
            GetExpectedDependencyGraphAsync,
            dependencyDetail,
            DefaultOutputFileName,
            true);
        }

        [Fact]
        public void ValidateIncompleteFullGraph()
        {
            DependencyDetail dependencyDetail = new DependencyDetail
            {
                Name = "Base.Asset",
                RepoUri = "RepoA",
                Commit = "shaA",
                Version = "1.0.0"
            };

            DependencyTestDriver.GetGraphAndCompare("DependencyGraph", async driver =>
            {
                return await driver.GetDependencyGraph(dependencyDetail);
            },
            GetExpectedDependencyGraphAsync,
            dependencyDetail,
            "output1.xml",
            false);
        }

        [Fact]
        public void ValidateGraphFromChildNode()
        {
            DependencyDetail dependencyDetail = new DependencyDetail
            {
                Name = "Base.Asset",
                RepoUri = "RepoB",
                Commit = "shaB",
                Version = "1.0.0"
            };

            DependencyTestDriver.GetGraphAndCompare("DependencyGraph", async driver =>
            {
                return await driver.GetDependencyGraph(dependencyDetail);
            },
            GetExpectedDependencyGraphAsync,
            dependencyDetail,
            DefaultOutputFileName,
            true);
        }

        [Fact]
        public void ValidateGraphWithNoChildNodes()
        {
            DependencyDetail dependencyDetail = new DependencyDetail
            {
                Name = "Base.Asset",
                RepoUri = "RepoX",
                Commit = "shaX",
                Version = "1.0.0"
            };

            DependencyTestDriver.GetGraphAndCompare("DependencyGraph", async driver =>
            {
                return await driver.GetDependencyGraph(dependencyDetail);
            },
            GetExpectedDependencyGraphAsync,
            dependencyDetail,
            DefaultOutputFileName,
            true);
        }

        [Fact]
        public void ValidateLeafNode()
        {
            DependencyDetail dependencyDetail = new DependencyDetail
            {
                Name = "Base.Asset",
                RepoUri = "RepoE",
                Commit = "shaE1",
                Version = "1.0.0"
            };

            DependencyTestDriver.GetGraphAndCompare("DependencyGraph", async driver =>
            {
                return await driver.GetDependencyGraph(dependencyDetail);
            },
            GetExpectedDependencyGraphAsync,
            dependencyDetail,
            DefaultOutputFileName,
            true);
        }

        private async Task<DependencyGraph> GetExpectedDependencyGraphAsync(DependencyDetail rootDependency, string temporaryRepositoryPath, string outputFileName)
        {
            HashSet<DependencyDetail> flatGraph = new HashSet<DependencyDetail>(new DependencyDetailComparer()) { rootDependency };
            DependencyGraphNode graphNode = new DependencyGraphNode(rootDependency);
            Stack<DependencyGraphNode> nodesToVisit = new Stack<DependencyGraphNode>();

            string outputFilePath = Path.Combine(
                    temporaryRepositoryPath,
                    rootDependency.RepoUri,
                    rootDependency.Commit,
                    outputFileName);

            if (!File.Exists(outputFilePath))
            {
                return new DependencyGraph(graphNode, flatGraph);
            }

            string output = await File.ReadAllTextAsync(outputFilePath);
            XmlDocument document = new XmlDocument();
            document.LoadXml(output);

            XmlNode baseNode = document.SelectSingleNode("Dependencies");
            Stack<XmlNode> xmlNodes = new Stack<XmlNode>();
            xmlNodes.Push(baseNode);

            nodesToVisit.Push(graphNode);

            while (nodesToVisit.Count > 0)
            {
                DependencyGraphNode node = nodesToVisit.Pop();
                baseNode = xmlNodes.Pop();

                foreach (XmlNode xmlNode in baseNode.ChildNodes)
                {
                    DependencyDetail dependencyDetail = new DependencyDetail
                    {
                        Commit = xmlNode.Attributes["Sha"].Value,
                        Name = xmlNode.Attributes["Name"].Value,
                        RepoUri = xmlNode.Attributes["Uri"].Value,
                        Version = xmlNode.Attributes["Version"].Value
                    };
                    DependencyGraphNode dependencyGraphNode = new DependencyGraphNode(dependencyDetail, node.VisitedNodes);
                    dependencyGraphNode.VisitedNodes.Add(node.DependencyDetail.RepoUri);
                    node.ChildNodes.Add(dependencyGraphNode);
                    nodesToVisit.Push(dependencyGraphNode);
                    flatGraph.Add(dependencyGraphNode.DependencyDetail);
                    xmlNodes.Push(xmlNode);
                }
            }

            return new DependencyGraph(graphNode, flatGraph);
        }
    }
}
