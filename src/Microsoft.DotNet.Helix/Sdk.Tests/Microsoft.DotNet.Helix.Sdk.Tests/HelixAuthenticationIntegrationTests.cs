// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Arcade.Test.Common;
using Microsoft.DotNet.Helix.Client;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.Helix.JobMonitor;
using Microsoft.DotNet.Helix.Sdk;
using Moq;
using Newtonsoft.Json;
using Xunit;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Microsoft.DotNet.Helix.Sdk.Tests;

public class HelixAuthenticationIntegrationTests
{
    [Fact]
    public void SendHelixJobExecutesCompleteEntraPathWithoutExposingInjectedPat()
    {
        var api = new Mock<IHelixApi>(MockBehavior.Strict);
        var typeDefinition = new Mock<IJobDefinitionWithType>(MockBehavior.Strict);
        var queueDefinition = new Mock<IJobDefinitionWithTargetQueue>(MockBehavior.Strict);
        var jobDefinition = new Mock<IJobDefinition>(MockBehavior.Strict);
        var commandDefinition = new Mock<IWorkItemDefinitionWithCommand>(MockBehavior.Strict);
        var payloadDefinition = new Mock<IWorkItemDefinitionWithPayload>(MockBehavior.Strict);
        var workItemDefinition = new Mock<IWorkItemDefinition>(MockBehavior.Strict);
        var sentJob = new Mock<ISentJob>(MockBehavior.Strict);

        typeDefinition.Setup(j => j.WithType("test")).Returns(queueDefinition.Object);
        queueDefinition.Setup(j => j.WithTargetQueue("internal.queue")).Returns(jobDefinition.Object);
        jobDefinition.Setup(j => j.WithMaxRetryCount(0)).Returns(jobDefinition.Object);
        jobDefinition
            .Setup(j => j.DefineWorkItem("work-item"))
            .Returns(commandDefinition.Object);
        commandDefinition
            .Setup(w => w.WithCommand(It.IsAny<string>()))
            .Returns(payloadDefinition.Object);
        payloadDefinition.Setup(w => w.WithEmptyPayload()).Returns(workItemDefinition.Object);
        workItemDefinition.Setup(w => w.AttachToJob()).Returns(jobDefinition.Object);
        jobDefinition
            .Setup(j => j.WithCorrelationPayloadDirectory(It.IsAny<string>(), ""))
            .Returns(jobDefinition.Object);
        jobDefinition
            .Setup(j => j.SendAsync(
                It.IsAny<Action<string>>(),
                It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sentJob.Object);
        sentJob.SetupGet(j => j.CorrelationId).Returns("job-id");
        sentJob.SetupGet(j => j.HelixCancellationToken).Returns("cancellation-token");

        var engine = new MockEngine { ContinueOnError = true };
        string requestedBaseUri = null;
        var workItem = new TaskItem("work-item");
        workItem.SetMetadata(SendHelixJob.MetadataNames.Command, "run-tests");
        var task = new SendHelixJob
        {
            BuildEngine = engine,
            Type = "Test",
            TargetQueue = "internal.queue",
            UseEntraAuthentication = true,
            AccessToken = "legacy-secret",
            WorkItems =
            [
                workItem,
            ],
            EntraHelixApiFactory = baseUri =>
            {
                requestedBaseUri = baseUri;
                return api.Object;
            },
            JobDefinitionFactory = selectedApi =>
            {
                Assert.Same(api.Object, selectedApi);
                return typeDefinition.Object;
            },
        };

        bool succeeded = task.Execute();

        Assert.True(succeeded);
        Assert.Equal("https://helix.dot.net/", requestedBaseUri);
        Assert.Equal("job-id", task.JobCorrelationId);
        Assert.Equal("cancellation-token", task.JobCancellationToken);
        Assert.DoesNotContain(
            engine.Messages.Select(message => message.Message)
                .Concat(engine.Warnings.Select(warning => warning.Message))
                .Concat(engine.Errors.Select(error => error.Message)),
            message => message?.Contains("legacy-secret", StringComparison.Ordinal) == true);
        jobDefinition.Verify(
            j => j.SendAsync(
                It.IsAny<Action<string>>(),
                It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task JobMonitorAuthenticatedRequestsRefreshExpiredEntraToken()
    {
        TimeSpan tokenLifetime = TimeSpan.FromMilliseconds(100);
        var credential = new ShortLivedTokenCredential(tokenLifetime);
        using var httpClient = FakeHttpClient.WithResponses(
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.OK));
        var options = new JobMonitorOptions
        {
            HelixBaseUri = "https://helix.dot.net/",
            UseEntraAuthentication = true,
        };

        var api = Assert.IsType<HelixApi>(
            JobMonitorRunner.CreateHelixApi(
                options,
                () => new HelixApi(new HelixApiOptions(
                    new Uri(options.HelixBaseUri),
                    credential)
                {
                    Transport = new HttpClientTransport(httpClient),
                })));

        string firstAuthorization = await SendAuthenticatedRequestAsync(api);
        await Task.Delay(tokenLifetime + TimeSpan.FromMilliseconds(300));
        string secondAuthorization = await SendAuthenticatedRequestAsync(api);

        Assert.Equal(2, credential.CallCount);
        Assert.NotEqual(firstAuthorization, secondAuthorization);
    }

    [Fact]
    public void EntraUploadedFileMetadataDoesNotSerializePatMaterial()
    {
        var files = ImmutableList.Create(
            new UploadedFile("results.trx", "https://storage/results.trx"));

        IImmutableList<UploadedFile> authenticatedFiles =
            GetHelixWorkItems.AddAccessTokenToFileLinks(
                files,
                "legacy-secret",
                useEntraAuthentication: true);
        string metadata = JsonConvert.SerializeObject(authenticatedFiles);

        Assert.DoesNotContain("legacy-secret", metadata, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token", metadata, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        "send-to-helix.yml",
        "parameters.HelixUseEntraAuthentication",
        "HelixAccessToken: ${{ parameters.HelixAccessToken }}",
        "AZURESUBSCRIPTION_SERVICE_CONNECTION_ID: ${{ parameters.HelixAzureSubscription }}",
        2)]
    [InlineData(
        "helix-job-monitor.yml",
        "parameters.useEntraAuthentication",
        "HELIX_ACCESSTOKEN: ${{ parameters.helixAccessToken }}",
        "AZURESUBSCRIPTION_SERVICE_CONNECTION_ID: ${{ parameters.azureSubscription }}",
        1)]
    public void EntraTemplateBoundaryOmitsPatAndForwardsWorkloadIdentity(
        string templateName,
        string entraParameter,
        string patEnvironmentVariable,
        string serviceConnectionEnvironmentVariable,
        int expectedEnvironmentCount)
    {
        string[] lines = ReadTemplateLines(templateName);
        string legacyGuard = $"${{{{ if eq({entraParameter}, false) }}}}:";
        string entraGuard = $"${{{{ if eq({entraParameter}, true) }}}}:";

        AssertEnvironmentEntriesGuardedBy(
            lines,
            patEnvironmentVariable,
            legacyGuard,
            expectedEnvironmentCount);
        AssertEnvironmentEntriesGuardedBy(
            lines,
            "AZURESUBSCRIPTION_CLIENT_ID: $(HelixEntraClientId)",
            entraGuard,
            expectedEnvironmentCount);
        AssertEnvironmentEntriesGuardedBy(
            lines,
            "AZURESUBSCRIPTION_TENANT_ID: $(HelixEntraTenantId)",
            entraGuard,
            expectedEnvironmentCount);
        AssertEnvironmentEntriesGuardedBy(
            lines,
            serviceConnectionEnvironmentVariable,
            entraGuard,
            expectedEnvironmentCount);
    }

    [Theory]
    [InlineData(
        "send-to-helix.yml",
        "- ${{ if and(eq(parameters.HelixUseEntraAuthentication, true), eq(parameters.HelixAzureSubscription, '')) }}:",
        "HelixAzureSubscription must be set when HelixUseEntraAuthentication is true.")]
    [InlineData(
        "helix-job-monitor.yml",
        "- ${{ if and(eq(parameters.useEntraAuthentication, true), eq(parameters.azureSubscription, '')) }}:",
        "azureSubscription must be set when useEntraAuthentication is true.")]
    public void EntraTemplatesRejectMissingWorkloadIdentityInput(
        string templateName,
        string validationGuard,
        string expectedError)
    {
        string[] lines = ReadTemplateLines(templateName);
        int guardIndex = Array.FindIndex(lines, line => line.Trim() == validationGuard);
        int errorIndex = Array.FindIndex(lines, line => line.Contains(
            $"throw \"{expectedError}\"",
            StringComparison.Ordinal));

        Assert.True(guardIndex >= 0, $"Validation guard was not found in {templateName}.");
        Assert.True(errorIndex > guardIndex, $"Validation error was not found after its guard in {templateName}.");
        int guardIndent = GetIndentation(lines[guardIndex]);
        Assert.DoesNotContain(
            lines[(guardIndex + 1)..errorIndex],
            line => !string.IsNullOrWhiteSpace(line) && GetIndentation(line) <= guardIndent);
    }

    private static async Task<string> SendAuthenticatedRequestAsync(HelixApi api)
    {
        using HttpMessage message = api.Pipeline.CreateMessage();
        message.Request.Method = RequestMethod.Get;
        message.Request.Uri.Reset(api.Options.BaseUri);
        await api.Pipeline.SendAsync(message, CancellationToken.None);
        Assert.True(message.Request.Headers.TryGetValue("Authorization", out string authorization));
        return authorization;
    }

    private static string[] ReadTemplateLines(string templateName)
        => File.ReadAllLines(
            Path.Combine(AppContext.BaseDirectory, "TemplateAssets", templateName));

    private static void AssertEnvironmentEntriesGuardedBy(
        string[] lines,
        string environmentEntry,
        string expectedGuard,
        int expectedCount)
    {
        int[] entryIndexes = lines
            .Select((line, index) => (line, index))
            .Where(item => item.line.Trim() == environmentEntry)
            .Select(item => item.index)
            .ToArray();

        Assert.Equal(expectedCount, entryIndexes.Length);
        foreach (int entryIndex in entryIndexes)
        {
            int entryIndent = GetIndentation(lines[entryIndex]);
            int guardIndex = entryIndex - 1;
            while (guardIndex >= 0
                && (string.IsNullOrWhiteSpace(lines[guardIndex])
                    || GetIndentation(lines[guardIndex]) >= entryIndent))
            {
                guardIndex--;
            }

            Assert.True(guardIndex >= 0, $"No guard found for '{environmentEntry}'.");
            Assert.Equal(expectedGuard, lines[guardIndex].Trim());
        }
    }

    private static int GetIndentation(string line)
        => line.Length - line.TrimStart().Length;

    private sealed class ShortLivedTokenCredential(TimeSpan lifetime) : TokenCredential
    {
        public int CallCount { get; private set; }

        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
            => new($"test-token-{++CallCount}", DateTimeOffset.UtcNow.Add(lifetime));

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
            => new(GetToken(requestContext, cancellationToken));
    }
}
