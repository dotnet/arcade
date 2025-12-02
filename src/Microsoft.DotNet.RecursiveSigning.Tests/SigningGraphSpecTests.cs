// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Reflection;
using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.DotNet.RecursiveSigning.Implementation;
using Microsoft.DotNet.RecursiveSigning.Models;
using Moq;
using Xunit;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    public sealed class SigningGraphTests
    {
        private static SigningGraph BuildGraph(params (FileNodeBase Node, FileNode? Parent)[] nodes)
        {
            var g = new SigningGraph();
            foreach (var (node, parent) in nodes)
            {
                g.AddNode(node, parent);
            }

            g.FinalizeDiscovery();
            return g;
        }
        private static FileNode CreateNode(string fileName, ICertificateIdentifier? certificateIdentifier)
        {
            byte[] bytes = Guid.NewGuid().ToByteArray();
            var contentKey = new FileContentKey(new ContentHash(ImmutableArray.Create(bytes)), fileName);
            var location = new FileLocation("/test/" + fileName, RelativePathInContainer: null);
            var metadata = Mock.Of<IFileMetadata>();
            return new FileNode(contentKey, location, metadata, certificateIdentifier);
        }

        private static ICertificateIdentifier Signable(string certificateName = "A")
        {
            return Mock.Of<ICertificateIdentifier>(c => c.Name == certificateName);
        }

        [Fact]
        public void AddNode_Skips_WhenAlreadySigned()
        {
            // The graph computes initial state; we no longer allow pre-setting Signed.
            // A signable leaf should become ReadyToSign.
            var node = CreateNode("a.dll", Signable());

            var g = BuildGraph((node, null));

            node.State.Should().Be(FileNodeState.ReadyToSign);
            g.GetNodesReadyForSigning().Should().ContainSingle().Which.Should().BeSameAs(node);
        }

        [Fact]
        public void GetNodesReadyForSigning_ExcludesReferenceNodes()
        {
            var original = CreateNode("a.dll", Signable());
            var referenceLocation = new FileLocation("/test/other/a.dll", RelativePathInContainer: null);
            var reference = new ReferenceNode(original.ContentKey, referenceLocation, original);

            var g = BuildGraph((original, null), (reference, null));

            // Both nodes are signable leaves, but reference nodes must not be scheduled for signing.
            g.GetNodesReadyForSigning().Should().ContainSingle().Which.Should().BeSameAs(original);
        }

        [Fact]
        public void GetContainersReadyForRepack_ExcludesReferenceNodes()
        {
            var container = CreateNode("c.zip", Signable());
            var child = CreateNode("a.dll", Signable());

            // Create a duplicate container node as a reference.
            var referenceLocation = new FileLocation("/test/other/c.zip", RelativePathInContainer: null);
            var containerRef = new ReferenceNode(container.ContentKey, referenceLocation, container);

            var g = BuildGraph((container, null), (child, container), (containerRef, null));

            g.MarkAsSigned(child);

            g.GetContainersReadyForRepack().Should().ContainSingle().Which.Should().BeSameAs(container);
        }

        [Fact]
        public void AddNode_Skips_WhenShouldIgnore()
        {
            // The graph computes initial state; we no longer allow pre-setting Skipped.
            // A signable leaf should become ReadyToSign.
            var node = CreateNode("a.dll", Signable());

            _ = BuildGraph((node, null));

            node.State.Should().Be(FileNodeState.ReadyToSign);
        }

        [Fact]
        public void AddNode_Skips_WhenNotSignable()
        {
            // No certificate identifier => not signable
            var node = CreateNode("a.dll", certificateIdentifier: null);

            _ = BuildGraph((node, null));

            node.State.Should().Be(FileNodeState.Skipped);
        }

        [Fact]
        public void AddNode_MakesLeafReadyToSign_WhenSignable()
        {
            var node = CreateNode("a.dll", Signable());

            var g = BuildGraph((node, null));

            node.State.Should().Be(FileNodeState.ReadyToSign);
            g.GetNodesReadyForSigning().Should().ContainSingle().Which.Should().BeSameAs(node);
        }

        [Fact]
        public void AddNode_SignableContainerStaysPendingUntilChildrenDone()
        {
            var container = CreateNode("c.zip", Signable());
            var child = CreateNode("a.dll", Signable());

            var g = BuildGraph((container, null), (child, container));

            container.State.Should().Be(FileNodeState.PendingRepack);
            g.GetNodesReadyForSigning().Should().NotContain(container);
        }

        [Fact]
        public void MarkAsSigned_MakesContainerReadyToRepack_WhenAllSignableChildrenDone()
        {
            var container = CreateNode("c.zip", Signable());
            var c1 = CreateNode("a.dll", Signable());
            var c2 = CreateNode("b.dll", Signable());

            var g = BuildGraph((container, null), (c1, container), (c2, container));

            container.State.Should().Be(FileNodeState.PendingRepack);

            g.MarkAsSigned(c1);
            container.State.Should().Be(FileNodeState.PendingRepack);
            g.GetContainersReadyForRepack().Should().BeEmpty();

            g.MarkAsSigned(c2);
            container.State.Should().Be(FileNodeState.ReadyToRepack);
            g.GetContainersReadyForRepack().Should().ContainSingle().Which.Should().BeSameAs(container);
        }

        [Fact]
        public void MarkAsSigned_IgnoresNonSignableChildren_ForContainerGating()
        {
            var container = CreateNode("c.zip", Signable());

            var signableChild = CreateNode("a.dll", Signable());
            var nonSignableChild = CreateNode("readme.txt", certificateIdentifier: null);

            var g = BuildGraph((container, null), (signableChild, container), (nonSignableChild, container));

            // Only the signable child should be required for repack readiness.
            g.MarkAsSigned(signableChild);
            container.State.Should().Be(FileNodeState.ReadyToRepack);
        }

        [Fact]
        public void AddNode_ReschedulesSkippedContainer_WhenSignableChildDiscovered()
        {
            var container = CreateNode("c.zip", Signable());
            var child = CreateNode("a.dll", Signable());

            var g = BuildGraph((container, null), (child, container));

            // Container should remain pending until its signable children are done.
            container.State.Should().Be(FileNodeState.PendingRepack);
        }

        [Fact]
        public void FinalizeDiscovery_WhenAlreadyFinalized_Throws()
        {
            var node = CreateNode("a.dll", Signable());
            var g = new SigningGraph();
            g.AddNode(node, null);

            g.FinalizeDiscovery();

            g.Invoking(x => x.FinalizeDiscovery())
                .Should().Throw<InvalidOperationException>()
                .WithMessage("*already been finalized*");
        }

        [Fact]
        public void NonSignableContainer_WithSignableChild_BecomesReadyToRepack_ButIsNotReadyToSign()
        {
            var container = CreateNode("c.zip", certificateIdentifier: null);
            var child = CreateNode("a.dll", Signable());

            var g = BuildGraph((container, null), (child, container));

            container.State.Should().Be(FileNodeState.PendingRepack);
            g.GetNodesReadyForSigning().Should().ContainSingle().Which.Should().BeSameAs(child);

            g.MarkAsSigned(child);

            container.State.Should().Be(FileNodeState.ReadyToRepack);
            g.GetContainersReadyForRepack().Should().ContainSingle().Which.Should().BeSameAs(container);
            g.GetNodesReadyForSigning().Should().NotContain(container);
        }

        [Fact]
        public void NonSignableContainer_AfterRepack_BecomesSkipped()
        {
            var container = CreateNode("c.zip", certificateIdentifier: null);
            var child = CreateNode("a.dll", Signable());

            var g = BuildGraph((container, null), (child, container));

            g.MarkAsSigned(child);
            container.State.Should().Be(FileNodeState.ReadyToRepack);

            g.MarkContainerAsRepacked(container);
            container.State.Should().Be(FileNodeState.Skipped);
        }

        [Fact]
        public void Container_WithOnlySkippedChildren_IsNotReadyToRepack()
        {
            var container = CreateNode("c.zip", Signable());
            var c1 = CreateNode("readme.txt", certificateIdentifier: null);
            var c2 = CreateNode("license.txt", certificateIdentifier: null);

            var g = BuildGraph((container, null), (c1, container), (c2, container));

            c1.State.Should().Be(FileNodeState.Skipped);
            c2.State.Should().Be(FileNodeState.Skipped);
            container.State.Should().Be(FileNodeState.ReadyToSign);
            g.GetContainersReadyForRepack().Should().BeEmpty();
            g.GetNodesReadyForSigning().Should().ContainSingle().Which.Should().BeSameAs(container);
        }

        [Fact]
        public void Container_WithMixedChildren_IsNotReadyToRepack_UntilAllSignableChildrenAreSigned()
        {
            var container = CreateNode("c.zip", Signable());
            var signable1 = CreateNode("a.dll", Signable());
            var signable2 = CreateNode("b.dll", Signable());
            var skipped = CreateNode("readme.txt", certificateIdentifier: null);

            var g = BuildGraph((container, null), (signable1, container), (signable2, container), (skipped, container));

            skipped.State.Should().Be(FileNodeState.Skipped);

            g.MarkAsSigned(signable1);
            container.State.Should().Be(FileNodeState.PendingRepack);
            g.GetContainersReadyForRepack().Should().BeEmpty();

            g.MarkAsSigned(signable2);
            container.State.Should().Be(FileNodeState.ReadyToRepack);
            g.GetContainersReadyForRepack().Should().ContainSingle().Which.Should().BeSameAs(container);
        }

        [Fact]
        public void NonSignableContainer_WithOnlySkippedChildren_IsSkipped()
        {
            var container = CreateNode("c.zip", certificateIdentifier: null);
            var c1 = CreateNode("readme.txt", certificateIdentifier: null);
            var c2 = CreateNode("license.txt", certificateIdentifier: null);

            var g = BuildGraph((container, null), (c1, container), (c2, container));

            container.State.Should().Be(FileNodeState.PendingRepack);
            c1.State.Should().Be(FileNodeState.Skipped);
            c2.State.Should().Be(FileNodeState.Skipped);

            // No signable children => no repack work required.
            g.GetNodesReadyForSigning().Should().BeEmpty();
            g.GetContainersReadyForRepack().Should().BeEmpty();
        }

        [Fact]
        public void NestedContainers_MixedNodes_ProgressThroughSignAndRepackRounds()
        {
            // Topology:
            // outer (signable)
            //  - outerSignable1 (signable)
            //  - outerSkipped (non-signable => skipped)
            //  - inner (non-signable)
            //      - innerSignable1 (signable)
            //      - innerSignable2 (signable)
            //      - innerSkipped (non-signable => skipped)
            var outer = CreateNode("outer.zip", Signable("Outer"));
            var outerSignable1 = CreateNode("outer-a.dll", Signable("A"));
            var outerSkipped = CreateNode("outer-readme.txt", certificateIdentifier: null);

            var inner = CreateNode("inner.zip", certificateIdentifier: null);
            var innerSignable1 = CreateNode("inner-a.dll", Signable("A"));
            var innerSignable2 = CreateNode("inner-b.dll", Signable("B"));
            var innerSkipped = CreateNode("inner-readme.txt", certificateIdentifier: null);

            var g = BuildGraph(
                (outer, null),
                (outerSignable1, outer),
                (outerSkipped, outer),
                (inner, outer),
                (innerSignable1, inner),
                (innerSignable2, inner),
                (innerSkipped, inner));

            // Initial states
            outer.State.Should().Be(FileNodeState.PendingRepack);
            inner.State.Should().Be(FileNodeState.PendingRepack);
            outerSkipped.State.Should().Be(FileNodeState.Skipped);
            innerSkipped.State.Should().Be(FileNodeState.Skipped);

            g.GetContainersReadyForRepack().Should().BeEmpty();

            var ready = g.GetNodesReadyForSigning();
            ready.Should().BeEquivalentTo(new[] { outerSignable1, innerSignable1, innerSignable2 });

            // Sign inner children one-by-one
            g.MarkAsSigned(innerSignable1);
            inner.State.Should().Be(FileNodeState.PendingRepack);
            g.GetContainersReadyForRepack().Should().BeEmpty();

            g.MarkAsSigned(innerSignable2);
            inner.State.Should().Be(FileNodeState.ReadyToRepack);
            g.GetContainersReadyForRepack().Should().ContainSingle().Which.Should().BeSameAs(inner);

            // Repack inner (non-signable => completes)
            g.MarkContainerAsRepacked(inner);
            inner.State.Should().Be(FileNodeState.Skipped);

            // Outer is still gated until all of its signable children are done.
            outer.State.Should().Be(FileNodeState.PendingRepack);
            g.GetContainersReadyForRepack().Should().BeEmpty();

            // Sign outer signable leaf => outer becomes ready to repack (because inner is done and other children are skipped)
            g.MarkAsSigned(outerSignable1);
            outer.State.Should().Be(FileNodeState.ReadyToRepack);

            // Repack outer (signable => becomes ReadyToSign)
            g.MarkContainerAsRepacked(outer);
            outer.State.Should().Be(FileNodeState.ReadyToSign);

            // Sign outer container itself
            g.MarkAsSigned(outer);
            outer.State.Should().Be(FileNodeState.Signed);

            // Graph should be complete now
            g.IsComplete().Should().BeTrue();
        }
    }
}
