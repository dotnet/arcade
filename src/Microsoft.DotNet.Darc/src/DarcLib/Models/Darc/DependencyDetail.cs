// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.DarcLib
{
    public class DependencyDetail
    {
        public string Branch { get; set; }

        public string Name { get; set; }

        public string Version { get; set; }

        public string RepoUri { get; set; }

        public string Commit { get; set; }

        public DependencyGraphNode ToGraphNode()
        {
            DependencyGraphNode graphNode = new DependencyGraphNode
            {
                DependencyDetail = this
            };

            return graphNode;
        }


        public DependencyGraphNode ToGraphNode(IEnumerable<DependencyDetail> childNodes)
        {
            DependencyGraphNode graphNode = ToGraphNode();
            graphNode.ChildNodes = new List<DependencyGraphNode>();

            foreach (DependencyDetail dependencyDetail in childNodes)
            {
                DependencyGraphNode childNode = new DependencyGraphNode();
                childNode.DependencyDetail = dependencyDetail;
                graphNode.ChildNodes.Add(childNode);
            }

            return graphNode;
        }
    }
}
