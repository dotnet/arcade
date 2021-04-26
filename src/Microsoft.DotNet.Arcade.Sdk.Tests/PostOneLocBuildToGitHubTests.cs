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

        private readonly string _locFilesDir = @"D:\a\1\a\loc\";
        private readonly string _sourcesDir = @"D:\a\1\s\";
        private readonly string _gitHubOrg = "dotnet";
        private readonly string _gitHubRepo = "arcade";
        private readonly string _gitHubBranch = "main";

        private readonly string _prDiffFileContent = @"D:\a\1\s\src\Microsoft.CodeAnalysis.Analyzers\Core\xlf\CodeAnalysisDiagnosticsResources.fr.xlf
D:\a\1\s\src\Microsoft.CodeAnalysis.Analyzers\Core\xlf\CodeAnalysisDiagnosticsResources.ja.xlf
D:\a\1\s\src\Microsoft.CodeAnalysis.Analyzers\Core\xlf\CodeAnalysisDiagnosticsResources.ko.xlf
D:\a\1\s\src\Microsoft.CodeAnalysis.Analyzers\Core\xlf\CodeAnalysisDiagnosticsResources.ru.xlf
D:\a\1\s\src\Microsoft.CodeAnalysis.Analyzers\Core\xlf\CodeAnalysisDiagnosticsResources.zh-Hans.xlf
D:\a\1\s\src\Microsoft.CodeAnalysis.Analyzers\Core\xlf\CodeAnalysisDiagnosticsResources.zh-Hant.xlf
D:\a\1\s\src\Microsoft.CodeAnalysis.BannedApiAnalyzers\Core\xlf\BannedApiAnalyzerResources.zh-Hans.xlf
D:\a\1\s\src\Microsoft.CodeAnalysis.BannedApiAnalyzers\Core\xlf\BannedApiAnalyzerResources.zh-Hant.xlf
D:\a\1\s\src\NetAnalyzers\Core\Microsoft.CodeQuality.Analyzers\xlf\MicrosoftCodeQualityAnalyzersResources.cs.xlf
D:\a\1\s\src\NetAnalyzers\Core\Microsoft.CodeQuality.Analyzers\xlf\MicrosoftCodeQualityAnalyzersResources.de.xlf
D:\a\1\s\src\NetAnalyzers\Core\Microsoft.CodeQuality.Analyzers\xlf\MicrosoftCodeQualityAnalyzersResources.es.xlf
D:\a\1\s\src\NetAnalyzers\Core\Microsoft.CodeQuality.Analyzers\xlf\MicrosoftCodeQualityAnalyzersResources.fr.xlf
D:\a\1\s\src\NetAnalyzers\Core\Microsoft.CodeQuality.Analyzers\xlf\MicrosoftCodeQualityAnalyzersResources.it.xlf
D:\a\1\s\src\NetAnalyzers\Core\Microsoft.CodeQuality.Analyzers\xlf\MicrosoftCodeQualityAnalyzersResources.ja.xlf
D:\a\1\s\src\NetAnalyzers\Core\Microsoft.CodeQuality.Analyzers\xlf\MicrosoftCodeQualityAnalyzersResources.ko.xlf
D:\a\1\s\src\NetAnalyzers\Core\Microsoft.CodeQuality.Analyzers\xlf\MicrosoftCodeQualityAnalyzersResources.pl.xlf
D:\a\1\s\src\NetAnalyzers\Core\Microsoft.CodeQuality.Analyzers\xlf\MicrosoftCodeQualityAnalyzersResources.pt-BR.xlf
D:\a\1\s\src\NetAnalyzers\Core\Microsoft.CodeQuality.Analyzers\xlf\MicrosoftCodeQualityAnalyzersResources.ru.xlf
D:\a\1\s\src\NetAnalyzers\Core\Microsoft.CodeQuality.Analyzers\xlf\MicrosoftCodeQualityAnalyzersResources.tr.xlf
D:\a\1\s\src\NetAnalyzers\Core\Microsoft.CodeQuality.Analyzers\xlf\MicrosoftCodeQualityAnalyzersResources.zh-Hans.xlf";

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
                SourcesDirectory = _sourcesDir,
                GitHubPat = "fake",
                GitHubOrg = _gitHubOrg,
                GitHubRepo = _gitHubRepo,
                GitHubBranch = _gitHubBranch,
            };
        }

        [Fact]
        public async Task MakesPr()
        {
            IReadOnlyList<PullRequest> prs = new List<PullRequest>
            {
                new TestPullRequest { TestTitle = $"A nonsense PR", TestState = ItemState.Open, TestBase = new TestGitReference { TestLabel = $"{_gitHubOrg}:{_gitHubBranch}" } },
                new TestPullRequest { TestTitle = $"{PostOneLocBuildToGitHub.PrPrefix}04/21/2021", TestState = ItemState.Open, TestBase = new TestGitReference { TestLabel = $"{_gitHubOrg}:{_gitHubBranch}" } },
                new TestPullRequest { TestTitle = $"A different nonsense PR", TestState = ItemState.Open, TestBase = new TestGitReference { TestLabel = $"{_gitHubOrg}:{_gitHubBranch}" } },
                new TestPullRequest { TestTitle = $"[DO NOT MERGE] Just testing", TestState = ItemState.Open, TestBase = new TestGitReference { TestLabel = $"{_gitHubOrg}:{_gitHubBranch}" } },
            };

            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>()))
                .Returns(true);
            _fileSystemMock.Setup(f => f.ReadFromFile(It.IsAny<string>()))
                .Returns(_prDiffFileContent);
            
            _gitHubClientMock
                .Setup(g => g.PullRequest.GetAllForRepository(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PullRequestRequest>()))
                .Returns(Task.FromResult(prs));

            await Task.Delay(10);
        }

        //[Fact]
        //public async Task TemporaryTestDeleteBeforeCommitting()
        //{
        //    PostOneLocBuildToGitHub task = new PostOneLocBuildToGitHub();
            
        //}

        [Fact]
        public async Task ReturnsExistingOneLocPr()
        {
            // Setup
            IReadOnlyList<PullRequest> prs = new List<PullRequest>
            {
                new TestPullRequest { TestTitle = $"A nonsense PR", TestState = ItemState.Open, TestBase = new TestGitReference { TestLabel = $"{_gitHubOrg}:{_gitHubBranch}" } },
                new TestPullRequest { TestTitle = $"{PostOneLocBuildToGitHub.PrPrefix}04/21/2021", TestState = ItemState.Open, TestBase = new TestGitReference { TestLabel = $"{_gitHubOrg}:{_gitHubBranch}" } },
                new TestPullRequest { TestTitle = $"A different nonsense PR", TestState = ItemState.Open, TestBase = new TestGitReference { TestLabel = $"{_gitHubOrg}:{_gitHubBranch}" } },
                new TestPullRequest { TestTitle = $"[DO NOT MERGE] Just testing", TestState = ItemState.Open, TestBase = new TestGitReference { TestLabel = $"{_gitHubOrg}:{_gitHubBranch}" } },
            };

            _gitHubClientMock
                .Setup(g => g.PullRequest.GetAllForRepository(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PullRequestRequest>()))
                .Returns(Task.FromResult(prs));

            // Act
            PullRequest foundPr = await _task.FindExistingOneLocPr(_gitHubClientMock.Object);

            // Verify
            foundPr.Title.Should().Be($"{PostOneLocBuildToGitHub.PrPrefix}04/21/2021");
            foundPr.Base.Label.Should().Be($"{_gitHubOrg}:{_gitHubBranch}");
        }

        [Fact]
        public async Task ReturnsNoLocPr()
        {
            // Setup
            IReadOnlyList<PullRequest> prs = new List<PullRequest>
            {
                new TestPullRequest { TestTitle = $"A nonsense PR", TestState = ItemState.Open, TestBase = new TestGitReference { TestLabel = $"{_gitHubOrg}:{_gitHubBranch}" } },
                new TestPullRequest { TestTitle = $"A different nonsense PR", TestState = ItemState.Open, TestBase = new TestGitReference { TestLabel = $"{_gitHubOrg}:{_gitHubBranch}" } },
                new TestPullRequest { TestTitle = $"[DO NOT MERGE] Just testing", TestState = ItemState.Open, TestBase = new TestGitReference { TestLabel = $"{_gitHubOrg}:{_gitHubBranch}" } },
            };

            _gitHubClientMock
                .Setup(g => g.PullRequest.GetAllForRepository(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PullRequestRequest>()))
                .Returns(Task.FromResult(prs));

            // Act
            PullRequest foundPr = await _task.FindExistingOneLocPr(_gitHubClientMock.Object);

            // Verify
            foundPr.Should().BeNull();
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
            public GitReference TestBase { get { return Base; } set { Base = value; } }
        }
        private class TestGitReference : GitReference
        {
            public string TestLabel { get { return Label; } set { Label = value; } }
        }
    }
}
