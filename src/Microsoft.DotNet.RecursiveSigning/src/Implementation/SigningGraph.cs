// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    /// <summary>
    /// Manages the signing dependency graph and determines signing order.
    /// Discovery phase:
    /// - Nodes can be added via <see cref="AddNode"/>.
    /// - No node state is considered finalized.
    /// Execution phase:
    /// - <see cref="FinalizeDiscovery"/> freezes discovery and computes initial node states.
    /// - Nodes transition via graph operations (e.g. <see cref="MarkAsSigned"/>).
    /// </summary>
    public sealed class SigningGraph : FileNodeGraph, ISigningGraph
    {
        private sealed class ContainerProgress
        {
            public int SignableChildCount;
            public int SignedOrSkippedSignableChildCount;
        }

        private readonly ConcurrentBag<FileNodeBase> _allNodes = new();
        private readonly Dictionary<FileNode, ContainerProgress> _containerProgress = new();
        private readonly object _lock = new();

        private bool _discoveryFinalized;

        public override bool IsDiscoveryFinalized => _discoveryFinalized;

        private static bool IsDone(FileNodeState state) =>
            state == FileNodeState.Signed || state == FileNodeState.Skipped;

        private bool IsDone(FileNode node)
        {
            if (IsDone(node.State))
            {
                return true;
            }

            if (node.State == FileNodeState.PendingRepack && node.CertificateIdentifier == null &&
                _containerProgress.TryGetValue(node, out var progress) && progress.SignableChildCount == 0)
            {
                return true;
            }

            return false;
        }

        private static FileNodeState ComputeInitialState(FileNodeBase node, int signableChildCount, int doneSignableChildCount)
        {
            if (node.Children.Count > 0)
            {
                if (signableChildCount > 0)
                {
                    // Container with signable children must wait for children, then repack.
                    return doneSignableChildCount >= signableChildCount ? FileNodeState.ReadyToRepack : FileNodeState.PendingRepack;
                }

                // No signable children => no repack is required.
                if (node.CertificateIdentifier != null)
                {
                    return FileNodeState.ReadyToSign;
                }

                // Non-signable container with no signable children stays tracked as PendingRepack.
                return FileNodeState.PendingRepack;
            }

            // Leaf nodes.
            return node.CertificateIdentifier != null ? FileNodeState.ReadyToSign : FileNodeState.Skipped;
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

                    int signableChildCount = 0;
                    int doneSignableChildCount = 0;

                    // Initialize tracking of all children.
                    if (node is FileNode container && container.Children.Count > 0)
                    {
                        signableChildCount = container.Children.OfType<FileNode>().Count(c => c.CertificateIdentifier != null);
                        doneSignableChildCount = container.Children.OfType<FileNode>().Count(c => c.CertificateIdentifier != null && IsDone(c.State));

                        if (!_containerProgress.TryGetValue(container, out var progress))
                        {
                            progress = new ContainerProgress();
                            _containerProgress[container] = progress;
                        }

                        progress.SignableChildCount = signableChildCount;
                        progress.SignedOrSkippedSignableChildCount = doneSignableChildCount;
                    }

                    node.InitializeState(ComputeInitialState(node, signableChildCount, doneSignableChildCount));
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

                if (container.State == FileNodeState.ReadyToRepack)
                {
                    // After repack, signable containers must be signed; non-signable containers are complete.
                    if (container.CertificateIdentifier != null)
                    {
                        container.MarkReadyToSign();
                    }
                    else
                    {
                        container.MarkSkipped();
                    }
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

        public void MarkAsSigned(FileNode node)
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

                node.MarkSigned();

                // Reference nodes are informational only and mirror canonical state (via property delegation).
                var parent = node.Parent;
                if (parent is FileNode parentFile && node.CertificateIdentifier != null)
                {
                    if (_containerProgress.TryGetValue(parentFile, out var progress))
                    {
                        progress.SignedOrSkippedSignableChildCount++;

                        if (progress.SignableChildCount > 0 &&
                            progress.SignedOrSkippedSignableChildCount >= progress.SignableChildCount &&
                            (parentFile.State == FileNodeState.PendingSigning || parentFile.State == FileNodeState.PendingRepack))
                        {
                            parentFile.MarkReadyToRepack();
                        }
                    }
                }
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
