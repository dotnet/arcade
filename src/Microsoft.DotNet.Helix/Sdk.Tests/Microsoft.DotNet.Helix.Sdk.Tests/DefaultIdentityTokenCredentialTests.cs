// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using Azure.Core;
using Azure.Identity;
using Microsoft.DotNet.ArcadeAzureIntegration;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests;

[Collection("NonParallel")]
public sealed class DefaultIdentityTokenCredentialTests
{
    private static readonly string[] s_environmentVariableNames =
    [
        "servicePrincipalId",
        "idToken",
        "tenantId",
        "AZURE_CLIENT_ID",
        "AZURE_TENANT_ID",
        "AZURE_FEDERATED_TOKEN_FILE",
        "HELIX_ENTRA_CLIENT_ID",
        "HELIX_ENTRA_TENANT_ID",
        "HELIX_ENTRA_TOKEN_FILE",
        "SYSTEM_ACCESSTOKEN",
        "SYSTEM_OIDCREQUESTURI",
        "AZURESUBSCRIPTION_CLIENT_ID",
        "AZURESUBSCRIPTION_TENANT_ID",
        "AZURESUBSCRIPTION_SERVICE_CONNECTION_ID",
    ];

    [Fact]
    public void EntraIssuedAssertionTakesPrecedenceOverAzurePipelinesCredential()
    {
        RunWithCredentialEnvironment(
            includeFederatedToken: true,
            credential =>
            {
                Assert.IsType<WorkloadIdentityCredential>(credential);
            });
    }

    [Fact]
    public void AzurePipelinesCredentialRemainsFallbackWithoutFederatedToken()
    {
        RunWithCredentialEnvironment(
            includeFederatedToken: false,
            credential =>
            {
                Assert.IsType<AzurePipelinesCredential>(credential);
            });
    }

    [Fact]
    public void ExistingDefaultStillPrefersAzurePipelinesCredential()
    {
        RunWithCredentialEnvironment(
            includeFederatedToken: true,
            credential =>
            {
                Assert.IsType<AzurePipelinesCredential>(credential);
            },
            preferWorkloadIdentityCredential: false);
    }

    [Fact]
    public void MissingFederatedTokenFileFallsBackToAzurePipelinesCredential()
    {
        RunWithCredentialEnvironment(
            includeFederatedToken: false,
            credential =>
            {
                Assert.IsType<AzurePipelinesCredential>(credential);
            },
            configureMissingTokenFile: true);
    }

    private static void RunWithCredentialEnvironment(
        bool includeFederatedToken,
        Action<TokenCredential> assertion,
        bool preferWorkloadIdentityCredential = true,
        bool configureMissingTokenFile = false)
    {
        string? tokenFile = null;
        string?[] originalValues = new string?[s_environmentVariableNames.Length];
        for (int i = 0; i < s_environmentVariableNames.Length; i++)
        {
            originalValues[i] = Environment.GetEnvironmentVariable(s_environmentVariableNames[i]);
        }

        try
        {
            foreach (string variableName in s_environmentVariableNames)
            {
                Environment.SetEnvironmentVariable(variableName, null);
            }

            if (includeFederatedToken)
            {
                tokenFile = Path.GetTempFileName();
                File.WriteAllText(tokenFile, "federated-token");
                Environment.SetEnvironmentVariable("HELIX_ENTRA_CLIENT_ID", "workload-client");
                Environment.SetEnvironmentVariable("HELIX_ENTRA_TENANT_ID", "tenant");
                Environment.SetEnvironmentVariable("HELIX_ENTRA_TOKEN_FILE", tokenFile);
            }
            else if (configureMissingTokenFile)
            {
                Environment.SetEnvironmentVariable("HELIX_ENTRA_CLIENT_ID", "workload-client");
                Environment.SetEnvironmentVariable("HELIX_ENTRA_TENANT_ID", "tenant");
                Environment.SetEnvironmentVariable("HELIX_ENTRA_TOKEN_FILE", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            }

            Environment.SetEnvironmentVariable("SYSTEM_ACCESSTOKEN", "system-token");
            Environment.SetEnvironmentVariable("SYSTEM_OIDCREQUESTURI", "https://dev.azure.com/oidc");
            Environment.SetEnvironmentVariable("AZURESUBSCRIPTION_CLIENT_ID", "pipeline-client");
            Environment.SetEnvironmentVariable("AZURESUBSCRIPTION_TENANT_ID", "tenant");
            Environment.SetEnvironmentVariable("AZURESUBSCRIPTION_SERVICE_CONNECTION_ID", "service-connection");

            TokenCredential credential = DefaultIdentityTokenCredential.CreateAvailableTokenCredential(
                new DefaultIdentityTokenCredentialOptions
                {
                    DisableShortCache = true,
                    ExcludeAzureCliCredential = true,
                    PreferWorkloadIdentityCredential = preferWorkloadIdentityCredential,
                });

            assertion(credential);
        }
        finally
        {
            for (int i = 0; i < s_environmentVariableNames.Length; i++)
            {
                Environment.SetEnvironmentVariable(s_environmentVariableNames[i], originalValues[i]);
            }

            if (tokenFile != null)
            {
                File.Delete(tokenFile);
            }
        }
    }
}
