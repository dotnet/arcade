// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.DotNet.RecursiveSigning.Implementation;
using Xunit;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    public sealed class ContainerHandlerRegistryTests
    {
        [Fact]
        public void FindHandler_ThrowsForEmptyPath()
        {
            var registry = new ContainerHandlerRegistry();

            Assert.Throws<ArgumentException>(() => registry.FindHandler(""));
        }

        [Fact]
        public void RegisterHandler_ThrowsForNullHandler()
        {
            var registry = new ContainerHandlerRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.RegisterHandler(null!));
        }

        [Fact]
        public void FindHandler_ReturnsNullWhenNoHandlerMatches()
        {
            var registry = new ContainerHandlerRegistry();
            registry.RegisterHandler(new StubContainerHandler());

            Assert.Null(registry.FindHandler("file.unknown"));
        }

        [Fact]
        public void FindHandler_ReturnsMatchingHandler()
        {
            var registry = new ContainerHandlerRegistry();
            var handler = new StubContainerHandler();
            registry.RegisterHandler(handler);

            Assert.Same(handler, registry.FindHandler("file.testcontainer"));
        }

        [Fact]
        public void FindHandler_ThrowsWhenMoreThanOneHandlerMatches()
        {
            var registry = new ContainerHandlerRegistry();
            registry.RegisterHandler(new StubContainerHandler());
            registry.RegisterHandler(new StubContainerHandler());

            var ex = Assert.Throws<InvalidOperationException>(() => registry.FindHandler("file.testcontainer"));
            Assert.Contains("More than one", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
