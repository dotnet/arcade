// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.DarcLib
{
    public class DependencyGraphNode
    {
        public DependencyGraphNode(DependencyDetail dependencyDetail)
            : this(
                  dependencyDetail,
                  new HashSet<string>())
        {
        }

        public DependencyGraphNode(
            DependencyDetail dependencyDetail,
            HashSet<string> visitedNodes)
            : this(
                dependencyDetail,
                visitedNodes,
                new HashSet<DependencyGraphNode>())
        {
        }

        public DependencyGraphNode(
            DependencyDetail dependencyDetail,
            HashSet<string> visitedNodes,
            HashSet<DependencyGraphNode> childNodes)
        {
            DependencyDetail = dependencyDetail;
            VisitedNodes = new HashSet<string>(visitedNodes);
            ChildNodes = new HashSet<DependencyGraphNode>(childNodes, new DependencyGraphNodeComparer());
        }

        public HashSet<string> VisitedNodes { get; set; }

        public DependencyDetail DependencyDetail { get; set; }

        public HashSet<DependencyGraphNode> ChildNodes { get; set; }
    }
}
