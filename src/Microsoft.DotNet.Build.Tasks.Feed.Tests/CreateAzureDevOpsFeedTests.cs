// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Arcade.Common;
using Microsoft.Arcade.Test.Common;
using Microsoft.DotNet.Build.Tasks.Feed.Tests.TestDoubles;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests;

public class CreateAzureDevOpsFeedTests
{
    private const string FeedUrl = "https://fakefeed.azure.com/nuget/v3/index.json";
    private const string PermissionsUrl = "https://fakefeed.azure.com/_apis/packaging/feeds/test/permissions";
    private const string PublisherDescriptor = "Microsoft.VisualStudio.Services.Claims.AadServicePrincipal;tenant\\publisher";

    [Fact]
    public async Task WaitForFeedReadyRetriesUntilPublishingCredentialCanReadFeed()
    {
        using var httpClient = FakeHttpClient.WithResponses(
            new HttpResponseMessage(HttpStatusCode.Forbidden),
            new HttpResponseMessage(HttpStatusCode.Forbidden),
            new HttpResponseMessage(HttpStatusCode.OK));
        var retryHandler = new MockRetryHandler(maxAttempts: 3);
        var buildEngine = new MockBuildEngine();
        var task = new CreateAzureDevOpsFeed { BuildEngine = buildEngine };

        bool result = await task.WaitForFeedReadyAsync(FeedUrl, httpClient, retryHandler);

        result.Should().BeTrue();
        retryHandler.ActualAttempts.Should().Be(3);
        buildEngine.BuildErrorEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task WaitForFeedReadyFailsAfterRetriesAreExhausted()
    {
        using var httpClient = FakeHttpClient.WithResponses(
            new HttpResponseMessage(HttpStatusCode.Forbidden),
            new HttpResponseMessage(HttpStatusCode.Forbidden));
        var retryHandler = new MockRetryHandler(maxAttempts: 2);
        var buildEngine = new MockBuildEngine();
        var task = new CreateAzureDevOpsFeed { BuildEngine = buildEngine };

        bool result = await task.WaitForFeedReadyAsync(FeedUrl, httpClient, retryHandler);

        result.Should().BeFalse();
        retryHandler.ActualAttempts.Should().Be(2);
        buildEngine.BuildErrorEvents.Should().ContainSingle();
    }

    [Fact]
    public async Task WaitForFeedPermissionsReadyIgnoresDirectContributorUntilComputedRoleIsEffective()
    {
        using var httpClient = FakeHttpClient.WithResponses(
            CreatePermissionsResponse(("contributor", false), ("none", true)),
            CreatePermissionsResponse(("contributor", false), ("reader", true)),
            CreatePermissionsResponse(("contributor", false), ("contributor", true)));
        var retryHandler = new MockRetryHandler(maxAttempts: 3);
        var buildEngine = new MockBuildEngine();
        var task = new CreateAzureDevOpsFeed { BuildEngine = buildEngine };
        var requiredPermissions = new[]
        {
            new AzureDevOpsFeedPermission(PublisherDescriptor, "contributor")
        };

        bool result = await task.WaitForFeedPermissionsReadyAsync(
            PermissionsUrl,
            requiredPermissions,
            httpClient,
            retryHandler);

        result.Should().BeTrue();
        retryHandler.ActualAttempts.Should().Be(3);
        buildEngine.BuildErrorEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task WaitForFeedPermissionsReadyFailsWhenPublisherRemainsReadOnly()
    {
        using var httpClient = FakeHttpClient.WithResponses(
            CreateEmptyPermissionsResponse(),
            CreatePermissionsResponse(("contributor", false), ("reader", true)),
            CreatePermissionsResponse(("contributor", false), ("reader", true)));
        var retryHandler = new MockRetryHandler(maxAttempts: 3);
        var buildEngine = new MockBuildEngine();
        var task = new CreateAzureDevOpsFeed { BuildEngine = buildEngine };
        var requiredPermissions = new[]
        {
            new AzureDevOpsFeedPermission(PublisherDescriptor, "contributor")
        };

        bool result = await task.WaitForFeedPermissionsReadyAsync(
            PermissionsUrl,
            requiredPermissions,
            httpClient,
            retryHandler);

        result.Should().BeFalse();
        retryHandler.ActualAttempts.Should().Be(3);
        buildEngine.BuildErrorEvents.Should().ContainSingle();
    }

    [Fact]
    public void FeedReadinessRetryBudgetAllowsSlowPermissionPropagation()
    {
        var task = new CreateAzureDevOpsFeed();

        var retryHandler = task.FeedReadinessRetryHandler.Should().BeOfType<ExponentialRetry>().Subject;
        retryHandler.MaxAttempts.Should().Be(10);
        retryHandler.MaximumDelay.Should().Be(TimeSpan.FromMinutes(2));
    }

    private static HttpResponseMessage CreateEmptyPermissionsResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"value\":[]}", Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage CreatePermissionsResponse(params (string Role, bool IsInheritedRole)[] permissions)
    {
        string entries = string.Join(
            ",",
            permissions.Select(permission =>
                $"{{\"identityDescriptor\":\"{PublisherDescriptor.Replace("\\", "\\\\")}\"," +
                $"\"role\":\"{permission.Role}\",\"isInheritedRole\":{permission.IsInheritedRole.ToString().ToLowerInvariant()}}}"));
        string response = $"{{\"value\":[{entries}]}}";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json")
        };
    }
}
