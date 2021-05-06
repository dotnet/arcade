#nullable enable
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

            var profileResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("profile content"),
            };

            var httpClient = FakeHttpClient.WithResponses(profileResponse, profileResponse, profileResponse);

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
                CreateAppBundle("apps/System.Foo.app", "ios-simulator-64_13.5"),
                CreateAppBundle("apps/System.Bar.app", "tvos-simulator-64"),
            });

            _downloadCount.Should().Be(0);
            _fileSystem.Files.Should().BeEmpty();
        }

        private ITaskItem CreateAppBundle(string path, string targets)
        {
            var mockBundle = new Mock<ITaskItem>();
            mockBundle.SetupGet(x => x.ItemSpec).Returns(path);
            mockBundle.Setup(x => x.GetMetadata(CreateXHarnessAppleWorkItems.TargetPropName)).Returns(targets);
            return mockBundle.Object;
        }
    }
}
