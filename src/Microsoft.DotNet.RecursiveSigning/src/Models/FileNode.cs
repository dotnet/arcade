// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Base node type for the signing graph.
    /// Holds only the information that is unique per occurrence/location.
    /// </summary>
    public abstract class FileNodeBase
    {
        /// <summary>
        /// Identity of the file based on content and file name.
        /// </summary>
        public FileContentKey ContentKey { get; internal set; }

        /// <summary>
        /// Location of the file on disk and (optionally) within a container.
        /// </summary>
        public FileLocation Location { get; internal set; }

        /// <summary>
        /// Parent node (container that contains this file), null for top-level files.
        /// </summary>
        public FileNode? Parent { get; internal set; }

        /// <summary>
        /// Child nodes (files contained in this container if this is a container).
        /// </summary>
        public virtual IReadOnlyList<FileNodeBase> Children => Array.Empty<FileNodeBase>();

        /// <summary>
        /// Whether this file is a container.
        /// </summary>
        public bool IsContainer => Children.Count > 0;

        public virtual ICertificateIdentifier? CertificateIdentifier { get; protected init; }

        public virtual FileNodeState State { get; protected set; }

        internal virtual void AttachToGraph(FileNodeGraph graph) => throw new NotSupportedException();

        internal virtual void InitializeState(FileNodeState initialState) => throw new NotSupportedException();

        internal virtual void MarkPendingSigning() => throw new NotSupportedException();

        internal virtual void MarkReadyToSign() => throw new NotSupportedException();

        internal virtual void MarkAsComplete() => throw new NotSupportedException();

        internal virtual void MarkReadyToRepack() => throw new NotSupportedException();

        internal virtual void MarkSkipped() => throw new NotSupportedException();

        protected FileNodeBase(FileContentKey contentKey, FileLocation location)
        {
            ContentKey = contentKey;
            Location = location ?? throw new ArgumentNullException(nameof(location));
            State = FileNodeState.None;
        }

        public override string ToString() => $"{ContentKey.FileName}";
    }

    /// <summary>
    /// Concrete node representing an actual file/container.
    /// </summary>
    public sealed class FileNode : FileNodeBase
    {
        private readonly List<FileNodeBase> _children = new();
        private readonly List<ReferenceNode> _referenceNodes = new();

        public override IReadOnlyList<FileNodeBase> Children => _children;

        internal IReadOnlyList<ReferenceNode> ReferenceNodes => _referenceNodes;

        internal void AddReferenceNode(ReferenceNode referenceNode)
        {
            if (referenceNode == null)
            {
                throw new ArgumentNullException(nameof(referenceNode));
            }

            _referenceNodes.Add(referenceNode);
        }

        /// <summary>
        /// Intrinsic file metadata.
        /// </summary>
        public IFileMetadata Metadata { get; internal set; }

        /// <summary>
        /// Certificate identifier used for signing this file.
        /// </summary>
        public override ICertificateIdentifier? CertificateIdentifier { get; protected init; }

        /// <summary>
        /// Whether this node requires signing.
        /// </summary>
        public bool NeedsSigning => State is not (FileNodeState.Complete or FileNodeState.Skipped or FileNodeState.PendingRepack or FileNodeState.ReadyToRepack);

        /// <summary>
        /// Current state of this node in the signing process.
        /// </summary>
        public override FileNodeState State { get; protected set; }

        internal FileNodeGraph? Graph { get; private set; }

        public FileNode(FileContentKey contentKey, FileLocation location, IFileMetadata metadata, ICertificateIdentifier? certificateIdentifier)
            : base(contentKey, location)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            CertificateIdentifier = certificateIdentifier;
        }

        internal override void AttachToGraph(FileNodeGraph graph)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (Graph != null && !ReferenceEquals(Graph, graph))
            {
                throw new InvalidOperationException("File node is already attached to a different graph.");
            }

            Graph = graph;
        }

        internal override void InitializeState(FileNodeState initialState)
        {
            EnsureGraphAttached();

            if (Graph!.IsDiscoveryFinalized)
            {
                throw new InvalidOperationException("Cannot initialize node state after discovery has been finalized.");
            }

            if (State != FileNodeState.None)
            {
                throw new InvalidOperationException($"Cannot initialize node state from {State}.");
            }

            State = initialState;
        }

        internal override void MarkPendingSigning() => TransitionTo(FileNodeState.PendingSigning);

        internal override void MarkReadyToSign() => TransitionTo(FileNodeState.ReadyToSign);

        internal override void MarkAsComplete() => TransitionTo(FileNodeState.Complete);

        internal override void MarkReadyToRepack() => TransitionTo(FileNodeState.ReadyToRepack);

        internal override void MarkSkipped() => TransitionTo(FileNodeState.Skipped);

        private void TransitionTo(FileNodeState nextState)
        {
            EnsureGraphAttached();

            if (!Graph!.IsDiscoveryFinalized)
            {
                throw new InvalidOperationException("Cannot change node state before discovery has been finalized.");
            }

            if (!IsValidTransition(State, nextState))
            {
                throw new InvalidOperationException($"Invalid node state transition {State} -> {nextState}.");
            }

            State = nextState;
        }

        private static bool IsValidTransition(FileNodeState from, FileNodeState to)
        {
            if (from == to)
            {
                return true;
            }

            if (from is FileNodeState.Complete or FileNodeState.Skipped)
            {
                return false;
            }

            return (from, to) switch
            {
                (FileNodeState.PendingSigning, FileNodeState.ReadyToSign) => true,
                (FileNodeState.PendingRepack, FileNodeState.ReadyToRepack) => true,
                (FileNodeState.ReadyToRepack, FileNodeState.ReadyToSign) => true,
                (FileNodeState.ReadyToRepack, FileNodeState.Complete) => true,
                (FileNodeState.ReadyToSign, FileNodeState.Complete) => true,
                _ => false,
            };
        }

        private void EnsureGraphAttached()
        {
            if (Graph == null)
            {
                throw new InvalidOperationException("File node is not attached to a signing graph.");
            }
        }

        public override string ToString() => $"{ContentKey.FileName} ({State})";
    }

    /// <summary>
    /// Placeholder for a reference occurrence when the canonical <see cref="FileNode"/> has not been discovered yet.
    /// This node is expected to be replaced during discovery dequeue once the canonical node exists.
    /// </summary>
    public sealed class ReferencePlaceholderNode : FileNodeBase
    {
        public override IReadOnlyList<FileNodeBase> Children => throw new NotSupportedException();

        public override ICertificateIdentifier? CertificateIdentifier => throw new NotSupportedException();

        public override FileNodeState State => throw new NotSupportedException();

        public ReferencePlaceholderNode(FileContentKey contentKey, FileLocation location)
            : base(contentKey, location)
        {
        }

        internal override void AttachToGraph(FileNodeGraph graph)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }
        }
    }

    /// <summary>
    /// Node that references another <see cref="FileNode"/>'s content (deduplication).
    /// Reference nodes should not be processed as independent signing/repack units.
    /// </summary>
    public sealed class ReferenceNode : FileNodeBase
    {
        public FileNode CanonicalNode { get; }

        public override IReadOnlyList<FileNodeBase> Children => CanonicalNode.Children;

        public override ICertificateIdentifier? CertificateIdentifier
        {
            get => CanonicalNode.CertificateIdentifier;
            protected init => throw new NotSupportedException();
        }

        public override FileNodeState State => CanonicalNode.State;

        public ReferenceNode(FileContentKey contentKey, FileLocation location, FileNode canonicalNode)
            : base(contentKey, location)
        {
            CanonicalNode = canonicalNode ?? throw new ArgumentNullException(nameof(canonicalNode));
            CanonicalNode.AddReferenceNode(this);
        }

        internal override void AttachToGraph(FileNodeGraph graph)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }
        }
    }

    public abstract class FileNodeGraph
    {
        public abstract bool IsDiscoveryFinalized { get; }
    }

    /// <summary>
    /// State of a file node in the signing process.
    /// </summary>
    public enum FileNodeState
    {
        /// <summary>
        /// Node state prior to graph discovery finalization.
        /// </summary>
        None,

        /// <summary>
        /// Not yet ready for signing. Pending child operations and/or repack
        /// </summary>
        PendingSigning,

        /// <summary>
        /// Waiting for repack. Pending child operations.
        /// </summary>
        PendingRepack,

        /// <summary>
        /// Ready to be signed.
        /// </summary>
        ReadyToSign,

        /// <summary>
        /// Complete (signed)
        /// </summary>
        Complete,

        /// <summary>
        /// A container is eligible to be repacked because all signable children are complete or skipped
        /// </summary>
        ReadyToRepack,

        /// <summary>
        /// Skipped (no processing required).
        /// </summary>
        Skipped
    }
}
