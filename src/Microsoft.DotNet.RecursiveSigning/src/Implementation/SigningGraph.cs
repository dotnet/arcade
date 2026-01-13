// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Models;
using NuGet.Packaging.Signing;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    /// <summary>
    /// Manages the signing dependency graph and determines signing order.
    /// Discovery phase:
    /// - Nodes can be added via <see cref="AddNode"/>.
    /// - No node state is considered finalized.
    /// Execution phase:
    /// - <see cref="FinalizeDiscovery"/> freezes discovery and computes initial node states.
    /// - Nodes transition via graph operations (e.g. <see cref="MarkAsComplete"/>).
    /// </summary>
    public sealed class SigningGraph : FileNodeGraph, ISigningGraph
    {
        private readonly ConcurrentBag<FileNodeBase> _allNodes = new();
        // Number of children requiring processing remaining for each node.
        private readonly Dictionary<FileNode, uint> _containerProgress = new();
        private readonly object _lock = new();

        private bool _discoveryFinalized;

        public override bool IsDiscoveryFinalized => _discoveryFinalized;

        private static bool IsDone(FileNodeState state) =>
            state == FileNodeState.Complete || state == FileNodeState.Skipped;

        private bool IsDone(FileNode node)
        {
            if (IsDone(node.State))
            {
                return true;
            }

            return false;
        }

        private static FileNodeState ComputeInitialState(FileNode node, uint childrenRequiringProcessing)
        {
            if (node.Children.Count > 0 && childrenRequiringProcessing > 0)
            {
                return FileNodeState.PendingRepack;
            }

            // Otherwise, all children are complete or there are no children,
            if (node.CertificateIdentifier != null)
            {
                // Signable container with all children complete, but already signed.
                return node.Metadata.IsAlreadySigned ? FileNodeState.Skipped : FileNodeState.ReadyToSign;
            }
            else
            {
                return FileNodeState.Skipped;
            }
        }

        public void AddNode(FileNodeBase node, FileNode? parent = null)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            lock (_lock)
            {
                if (_discoveryFinalized)
                {
                    throw new InvalidOperationException("Cannot add nodes after discovery has been finalized.");
                }

                node.AttachToGraph(this);

                _allNodes.Add(node);

                if (parent != null)
                {
                    node.Parent = parent;
                    if (parent is FileNode parentFile)
                    {
                        ((List<FileNodeBase>)parentFile.Children).Add(node);
                    }
                }
            }
        }

        public void FinalizeDiscovery()
        {
            lock (_lock)
            {
                if (_discoveryFinalized)
                {
                    throw new InvalidOperationException("Discovery has already been finalized.");
                }

                // Compute initial state in bottom-up order such that when a node is processed,
                // all of its children have already been processed (including reference nodes).
                // This enables correct container state/progress computation without additional passes.
                var orderedNodes = TopologicallyOrderByChildrenFirst(_allNodes);

                // Single bottom-up pass: by the time a node is processed, all its children have already been processed.
                // That means container progress and the node's initial state (including Ready*) can be computed immediately.
                foreach (var node in orderedNodes)
                {
                    if (node is ReferenceNode)
                    {
                        continue;
                    }

                    if (node is not FileNode concreteNode)
                    {
                        throw new NotImplementedException("Unexpected node type in signing graph.");
                    }

                    uint childrenRequiringProcessing = 0;
                    // Initialize tracking of all children.
                    if (concreteNode.Children.Count > 0)
                    {
                        // Track all children (including reference nodes) for gating purposes. Initially
                        // look for all children that are not skipped.
                        childrenRequiringProcessing = (uint)concreteNode.Children.Count(c => !IsDone(c.State));

                        if (_containerProgress.TryGetValue(concreteNode, out var progress))
                        {
                            throw new InvalidOperationException("Container progress already initialized for node.");
                        }

                        _containerProgress[concreteNode] = childrenRequiringProcessing;
                    }

                    concreteNode.InitializeState(ComputeInitialState(concreteNode, childrenRequiringProcessing));
                }

                _discoveryFinalized = true;
            }
        }

        private static IReadOnlyList<FileNodeBase> TopologicallyOrderByChildrenFirst(IEnumerable<FileNodeBase> nodes)
        {
            var ordered = new List<FileNodeBase>();
            var visited = new HashSet<FileNodeBase>();
            var visiting = new HashSet<FileNodeBase>();

            void Visit(FileNodeBase node)
            {
                if (visited.Contains(node))
                {
                    return;
                }

                if (!visiting.Add(node))
                {
                    throw new InvalidOperationException("Signing graph contains a cycle in parent/child relationships.");
                }

                foreach (var child in node.Children)
                {
                    Visit(child);
                }

                visiting.Remove(node);
                visited.Add(node);
                ordered.Add(node);
            }

            foreach (var node in nodes)
            {
                Visit(node);
            }

            return ordered;
        }

        public void MarkContainerAsRepacked(FileNode container)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            lock (_lock)
            {
                if (!_discoveryFinalized)
                {
                    throw new InvalidOperationException("Discovery must be finalized before marking containers as repacked.");
                }

                // If the container's cert identifier is null, it means it does not require signing.

                if (container.CertificateIdentifier != null)
                {
                    container.MarkReadyToSign();
                }
                else
                {
                    MarkAsComplete(container);
                }
            }
        }

        public IReadOnlyList<FileNode> GetNodesReadyForSigning()
        {
            lock (_lock)
            {
                if (!_discoveryFinalized)
                {
                    throw new InvalidOperationException("Discovery must be finalized before querying ready-to-sign nodes.");
                }

                return _allNodes.OfType<FileNode>().Where(n => n.State == FileNodeState.ReadyToSign).ToList();
            }
        }

        public IReadOnlyList<FileNode> GetContainersReadyForRepack()
        {
            lock (_lock)
            {
                if (!_discoveryFinalized)
                {
                    throw new InvalidOperationException("Discovery must be finalized before querying ready-to-repack containers.");
                }

                return _allNodes.OfType<FileNode>().Where(n => n.State == FileNodeState.ReadyToRepack).ToList();
            }
        }

        public void MarkAsComplete(FileNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            lock (_lock)
            {
                if (!_discoveryFinalized)
                {
                    throw new InvalidOperationException("Discovery must be finalized before marking nodes as signed.");
                }

                node.MarkAsComplete();

                // Update all parent containers that contain this canonical node, either directly or via references.
                // This relies on the canonical node tracking its reference occurrences.
                var parentsToUpdate = new HashSet<FileNode>();

                if (node.Parent is FileNode directParent)
                {
                    UpdateContainerTracking(directParent);
                }

                // Add all referenced node's parents if they exist.
                foreach (var reference in node.ReferenceNodes)
                {
                    if (reference.Parent is FileNode referenceParent)
                    {
                        UpdateContainerTracking(referenceParent);
                    }
                }
            }
        }

        private void UpdateContainerTracking(FileNode container)
        {
            if (!_containerProgress.TryGetValue(container, out var remainingChildrenToProcess))
            {
                throw new InvalidOperationException("Container progress not found for updating.");
            }

            _containerProgress[container] = remainingChildrenToProcess - 1;

            if (_containerProgress[container] == 0)
            {
                container.MarkReadyToRepack();
            }
        }

        public IReadOnlyList<FileNodeBase> GetAllNodes()
        {
            lock (_lock)
            {
                return _allNodes.ToList();
            }
        }

        public bool IsComplete()
        {
            lock (_lock)
            {
                if (!_discoveryFinalized)
                {
                    throw new InvalidOperationException("Discovery must be finalized before checking completion.");
                }

                foreach (var node in _allNodes)
                {
                    if (node is ReferenceNode reference)
                    {
                        if (!IsDone(reference.CanonicalNode))
                        {
                            return false;
                        }
                        continue;
                    }

                    if (node is FileNode fileNode)
                    {
                        if (!IsDone(fileNode))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }
    }
}
