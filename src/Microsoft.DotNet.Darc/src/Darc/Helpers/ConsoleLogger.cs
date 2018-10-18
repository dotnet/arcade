// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DarcLib;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Darc.Helpers
{
    /// <summary>
    /// Provides a shared and standarized way of logging different "things" or "objects" so regardless of where we are logging a thing to
    /// the console the UX will be the same.
    /// </summary>
    internal class ConsoleLogger
    {
        private readonly HashSet<string> _flatList = new HashSet<string>();

        public void LogDependency(DependencyDetail dependency, bool isFlat, string indent = "")
        {
            if (!isFlat)
            {
                Console.WriteLine($"{indent}- Name:    {dependency.Name}");
                Console.WriteLine($"{indent}  Version: {dependency.Version}");
                Console.WriteLine($"{indent}  Repo:    {dependency.RepoUri}");
                Console.WriteLine($"{indent}  Commit:  {dependency.Commit}");
            }
            else
            {
                string combo = $"{dependency.RepoUri} - {dependency.Commit}";

                if (!_flatList.Contains(combo))
                {
                    Console.WriteLine($"- Repo:    {dependency.RepoUri}");
                    Console.WriteLine($"  Commit:  {dependency.Commit}");

                    _flatList.Add(combo);
                }
            }
        }

        public void LogDependencyGraph(List<DependencyGraphNode> graph, bool isFlat, string indent = "")
        {
            foreach (DependencyGraphNode node in graph)
            {
                LogDependency(node.DependencyDetail, isFlat, indent);

                if (node.ChildNodes != null)
                {
                    LogDependencyGraph(node.ChildNodes, isFlat, indent + "  ");
                }
            }
        }
    }
}
