// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.DarcLib
{
    public class DependencyGraphNode
    {
        public DependencyGraphNode(DependencyDetail dependencyDetail)
        {
            DependencyDetail = dependencyDetail;
            ParentStack = new HashSet<string>();
            ChildNodes = new List<DependencyGraphNode>();
        }

        public DependencyGraphNode(DependencyDetail dependencyDetail, HashSet<string> parentStack) :
            this(dependencyDetail)
        {
            ParentStack = parentStack;
        }

        public DependencyGraphNode(DependencyDetail dependencyDetail, List<DependencyGraphNode> childNodes) :
            this(dependencyDetail)
        {
            ChildNodes = childNodes;
        }

        public DependencyGraphNode(DependencyDetail dependencyDetail, HashSet<string> parentStack, List<DependencyGraphNode> childNodes) :
            this(dependencyDetail, parentStack)
        {
            ChildNodes = childNodes;
        }

        public HashSet<string> ParentStack { get; set; }

        public DependencyDetail DependencyDetail { get; set; }

        public List<DependencyGraphNode> ChildNodes { get; set; }

        public override int GetHashCode()
        {
            return DependencyDetail.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            var item = obj as DependencyGraphNode;

            if (item == null)
            {
                return false;
            }

            return this.DependencyDetail == item.DependencyDetail;
        }
    }
}
