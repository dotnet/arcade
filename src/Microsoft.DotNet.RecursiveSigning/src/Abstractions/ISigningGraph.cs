// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Abstractions
{
    /// <summary>
    /// Manages the signing dependency graph and determines signing order.
    /// </summary>
    public interface ISigningGraph
    {
        /// <summary>
        /// Add a file node to the graph during discovery.
        /// </summary>
        void AddNode(FileNodeBase node, FileNode? parent = null);

        /// <summary>
        /// Completes discovery, freezing the graph and computing initial node states.
        /// After this is called, no new nodes can be added.
        /// </summary>
        void FinalizeDiscovery();

        /// <summary>
        /// Get all file nodes ready for signing (all dependencies satisfied).
        /// </summary>
        /// <returns>Nodes ready for signing.</returns>
        IReadOnlyList<FileNode> GetNodesReadyForSigning();

        /// <summary>
        /// Get all container nodes ready to be repacked (all signable children signed).
        /// </summary>
        /// <returns>Containers ready for repack.</returns>
        IReadOnlyList<FileNode> GetContainersReadyForRepack();

        /// <summary>
        /// Mark a node as signed and update dependent nodes.
        /// </summary>
        /// <param name="node">Node that was signed.</param>
        void MarkAsComplete(FileNode node);

        /// <summary>
        /// Mark a container as repacked, transitioning it so it can be signed.
        /// </summary>
        /// <param name="container">The container that was repacked.</param>
        void MarkContainerAsRepacked(FileNode container);

        /// <summary>
        /// Get all nodes in the graph.
        /// </summary>
        /// <returns>All nodes.</returns>
        IReadOnlyList<FileNodeBase> GetAllNodes();

        /// <summary>
        /// Check if all nodes are in a terminal state (signed or skipped).
        /// </summary>
        /// <returns>True if complete.</returns>
        bool IsComplete();
    }
}
