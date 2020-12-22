// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Build.Tasks.VersionTools;
using Microsoft.DotNet.Internal.DependencyInjection.Testing;
using Xunit;

namespace Microsoft.DotNet.VersionTools.Tasks.Tests
{
    public class UpdatePublishedVersionsTests
    {
        [Fact]
        public void AreDependenciesRegistered()
        {
            UpdatePublishedVersions task = new UpdatePublishedVersions();

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
