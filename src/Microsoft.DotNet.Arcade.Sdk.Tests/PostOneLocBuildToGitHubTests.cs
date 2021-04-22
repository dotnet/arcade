// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Arcade.Common;
using Microsoft.Arcade.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.Arcade.Sdk.Tests
{
    public class PostOneLocBuildToGitHubTests
    {
        private readonly Mock<IGitHubClient> _gitHubClientMock;
        private readonly Mock<IFileSystem> _fileSystemMock;
        private readonly Mock<IHelpers> _helpersMock;

        private readonly string _locFilesDir = @"C:\a\loc";
        private readonly string _gitHubOrg = "dotnet";
        private readonly string _gitHubRepo = "arcade";

        private readonly PostOneLocBuildToGitHub _task;

        public PostOneLocBuildToGitHubTests()
        {
            _fileSystemMock = new Mock<IFileSystem>();
            _gitHubClientMock = new Mock<IGitHubClient>();

            bool succeeded = false;

            _helpersMock = new Mock<IHelpers>();
            _helpersMock
                .Setup(x => x.DirectoryMutexExec(It.IsAny<Func<bool>>(), It.IsAny<string>()))
                .Callback<Func<bool>, string>((function, path) => {
                    succeeded = function();
                })
                .Returns(() => succeeded);


            _task = new PostOneLocBuildToGitHub
            {
                LocFilesDirectory = _locFilesDir,
                GitHubPat = "fake",
                GitHubOrg = _gitHubOrg,
                GitHubRepo = _gitHubRepo,
            };
        }

        [Fact]
        public async Task ReturnsExistingOneLocPr()
        {
            // Setup
            IReadOnlyList<PullRequest> prs = new List<PullRequest>
            {
                new TestPullRequest { TestTitle = $"A nonsense PR", TestState = ItemState.Open },
                new TestPullRequest { TestTitle = $"{PostOneLocBuildToGitHub.PrPrefix}04/21/2021", TestState = ItemState.Open },
                new TestPullRequest { TestTitle = $"A different nonsense PR", TestState = ItemState.Open },
                new TestPullRequest { TestTitle = $"[DO NOT MERGE] Just testing", TestState = ItemState.Open },
            };

            _gitHubClientMock
                .Setup(g => g.PullRequest.GetAllForRepository(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PullRequestRequest>()))
                .Returns(Task.FromResult(prs));

            // Act
            PullRequest foundPr = await _task.FindExistingOneLocPr(_gitHubClientMock.Object);

            // Verify
            foundPr.Title.Should().Be($"{PostOneLocBuildToGitHub.PrPrefix}04/21/2021");
        }

        private IServiceCollection CreateMockServiceCollection()
        {
            var collection = new ServiceCollection();
            collection.AddSingleton(_gitHubClientMock.Object);
            collection.AddSingleton(_fileSystemMock.Object);
            collection.AddSingleton(_helpersMock.Object);
            return collection;
        }

        private class TestPullRequest : PullRequest
        {
            public string TestTitle { get { return Title; } set { Title = value; } }
            public StringEnum<ItemState> TestState { get { return State; } set { State = value; } }
        }
    }
}
