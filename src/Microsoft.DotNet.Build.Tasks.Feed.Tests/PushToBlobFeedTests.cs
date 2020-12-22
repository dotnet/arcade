// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Internal.DependencyInjection.Testing;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public class PushToBlobFeedTests
    {
        [Fact]
        public void AreDependenciesRegistered()
        {
            PushToBlobFeed task = new PushToBlobFeed();

            DependencyInjectionValidation.IsDependencyResolutionCoherent(
                    s =>
                    {
                        task.ConfigureServices(s);
                    },
                    out string message
                )
                .Should()
                .BeTrue(message);
        }
    }
}
