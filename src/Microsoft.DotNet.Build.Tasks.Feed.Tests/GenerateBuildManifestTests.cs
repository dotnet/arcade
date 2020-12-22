// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Internal.DependencyInjection.Testing;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public class GenerateBuildManifestTests
    {
        [Fact]
        public void AreDependenciesRegistered()
        {
            GenerateBuildManifest task = new GenerateBuildManifest();

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
