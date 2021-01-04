// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Build.Tasks.VersionTools;
using Microsoft.DotNet.Internal.DependencyInjection.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.DotNet.VersionTools.Tasks.Tests
{
    public class UpdatePublishedVersionsTests
    {
        [Fact]
        public void AreDependenciesRegistered()
        {
            UpdatePublishedVersions task = new UpdatePublishedVersions();

            var collection = new ServiceCollection();
            task.ConfigureServices(collection);
            var provider = collection.BuildServiceProvider();

            foreach (var dependency in task.GetExecuteParameterTypes())
            {
                var service = provider.GetRequiredService(dependency);
                service.Should().NotBeNull();
            }

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
