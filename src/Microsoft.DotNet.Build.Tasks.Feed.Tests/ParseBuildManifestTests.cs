// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Internal.DependencyInjection.Testing;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public class ParseBuildManifestTests
    {
        [Fact]
        public void AreDependenciesRegistered()
        {
            ParseBuildManifest task = new ParseBuildManifest();

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
