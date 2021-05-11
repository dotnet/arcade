// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Arcade.Common;
using Microsoft.Arcade.Test.Common;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Arcade.Test.Common;
using Moq;
using Xunit;

#nullable enable
namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class ProvisioningProfileProviderTests
    {
        private readonly MockFileSystem _fileSystem;
        private readonly Mock<IHelpers> _helpersMock;
        private readonly ProvisioningProfileProvider _profileProvider;
        private int _downloadCount = 0;

        public ProvisioningProfileProviderTests()
        {
            _helpersMock = new Mock<IHelpers>();
            _helpersMock
                .Setup(x => x.DirectoryMutexExec(It.IsAny<Func<System.Threading.Tasks.Task>>(), It.IsAny<string>()))
                .Callback<Func<System.Threading.Tasks.Task>, string>((function, path) => {
                    ++_downloadCount;
                    function().GetAwaiter().GetResult();
                });

            _fileSystem = new MockFileSystem();

            var response1 = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("iOS content"),
            };

            var response2 = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("tvOS content"),
            };

            var httpClient = FakeHttpClient.WithResponses(response1, response2);

            _profileProvider = new ProvisioningProfileProvider(
                new TaskLoggingHelper(new MockBuildEngine(), nameof(ProvisioningProfileProviderTests)),
                _helpersMock.Object,
                _fileSystem,
                httpClient,
                "https://netcorenativeassets.azure.com/profiles/{PLATFORM}.mobileprovision",
                "/tmp");
        }

        [Fact]
        public void NonDeviceTargetsAreIgnored()
        {
            _profileProvider.AddProfilesToBundles(new[]
            {
                CreateAppBundle("/apps/System.Foo.app", "ios-simulator-64_13.5"),
                CreateAppBundle("/apps/System.Bar.app", "tvos-simulator-64"),
            });

            _downloadCount.Should().Be(0);
            _fileSystem.Files.Should().BeEmpty();
        }

        [Fact]
        public void MultipleiOSDeviceTargetsGetTheSameProfile()
        {
            _profileProvider.AddProfilesToBundles(new[]
            {
                CreateAppBundle("/apps/System.Device1.app", "ios-device"),
                CreateAppBundle("/apps/System.Simulator.app", "tvos-simulator-64"),
                CreateAppBundle("/apps/System.Device2.app", "ios-device"),
                CreateAppBundle("/apps/System.Foo.app", "ios-simulator-64_13.5"),
            });

            _downloadCount.Should().Be(1);

            _fileSystem.Files.Keys.Should().BeEquivalentTo(
                "/tmp/iOS.mobileprovision",
                "/apps/System.Device1.app/embedded.mobileprovision",
                "/apps/System.Device2.app/embedded.mobileprovision");

            _fileSystem.Files["/apps/System.Device1.app/embedded.mobileprovision"].Should().Be("iOS content");
            _fileSystem.Files["/apps/System.Device2.app/embedded.mobileprovision"].Should().Be("iOS content");
        }

        [Fact]
        public void MultiplePlatformsGetTheirProfile()
        {
            _profileProvider.AddProfilesToBundles(new[]
            {
                CreateAppBundle("/apps/System.Device1.iOS.app", "ios-device"),
                CreateAppBundle("/apps/System.Simulator.app", "tvos-simulator-64"),
                CreateAppBundle("/apps/System.Device2.iOS.app", "ios-device"),
                CreateAppBundle("/apps/System.Device3.tvOS.app", "tvos-device"),
            });

            _downloadCount.Should().Be(2);

            _fileSystem.Files.Keys.Should().BeEquivalentTo(
                "/tmp/iOS.mobileprovision",
                "/tmp/tvOS.mobileprovision",
                "/apps/System.Device1.iOS.app/embedded.mobileprovision",
                "/apps/System.Device2.iOS.app/embedded.mobileprovision",
                "/apps/System.Device3.tvOS.app/embedded.mobileprovision");

            _fileSystem.Files["/apps/System.Device1.iOS.app/embedded.mobileprovision"].Should().Be("iOS content");
            _fileSystem.Files["/apps/System.Device2.iOS.app/embedded.mobileprovision"].Should().Be("iOS content");
            _fileSystem.Files["/apps/System.Device3.tvOS.app/embedded.mobileprovision"].Should().Be("tvOS content");
        }

        [Fact]
        public void BundlesContainingProfileAreIgnored()
        {
            _fileSystem.WriteToFile("/apps/System.Device1.app/embedded.mobileprovision", "iOS content");
            _profileProvider.AddProfilesToBundles(new[]
            {
                CreateAppBundle("/apps/System.Device1.app", "ios-device"),
                CreateAppBundle("/apps/System.Simulator.app", "tvos-simulator-64"),
            });

            _downloadCount.Should().Be(0);
        }

        private static ITaskItem CreateAppBundle(string path, string targets)
        {
            var mockBundle = new Mock<ITaskItem>();
            mockBundle.SetupGet(x => x.ItemSpec).Returns(path);
            mockBundle.Setup(x => x.GetMetadata(CreateXHarnessAppleWorkItems.MetadataNames.Targets)).Returns(targets);
            return mockBundle.Object;
        }
    }
}
