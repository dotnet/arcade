// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Azure.Core;
using Azure.Identity;
using System.Threading.Tasks;
using System.Threading;

namespace Microsoft.DotNet.ArcadeAzureIntegration;


// This implementation of TokenCredential will try to cover all common ways of
// authentication to Azure services used in Arcade tooling
public class DefaultIdentityTokenCredential : TokenCredential
{
    private readonly TokenCredential _tokenCredential;

    public DefaultIdentityTokenCredential()
        : this(new DefaultIdentityTokenCredentialOptions())
    {
    }

    public DefaultIdentityTokenCredential(DefaultIdentityTokenCredentialOptions options)
    {
        _tokenCredential = CreateAvailableTokenCredential(options);
    }

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return _tokenCredential.GetTokenAsync(requestContext, cancellationToken);
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return _tokenCredential.GetToken(requestContext, cancellationToken);
    }

    internal static TokenCredential CreateAvailableTokenCredential(DefaultIdentityTokenCredentialOptions options)
    {
        TokenCredential? workloadIdentityCredential = GetWorkloadIdentityCredentialForAzurePipelineTask();
        TokenCredential? azurePipelinesCredential = GetAzurePipelinesCredentialForAzurePipelineTask();

        if (options.UseAzurePipelineCredentialAloneIfConfigured)
        {
            TokenCredential? pipelineCredential = options.PreferWorkloadIdentityCredential
                ? workloadIdentityCredential ?? azurePipelinesCredential
                : azurePipelinesCredential;
            if (pipelineCredential != null)
            {
                if (!options.DisableShortCache)
                {
                    return new TokenCredentialShortCache(pipelineCredential);
                }
                return pipelineCredential;
            }
        }

        List<TokenCredential> tokenCredentials = [];

        if (options.PreferWorkloadIdentityCredential)
        {
            AddCredential(workloadIdentityCredential);
            AddCredential(azurePipelinesCredential);
        }
        else
        {
            AddCredential(azurePipelinesCredential);
        }

        // Add Managed Identity credential
        ManagedIdentityId managedIdentityId = string.IsNullOrEmpty(options.ManagedIdentityClientId)
            ? ManagedIdentityId.SystemAssigned
            : ManagedIdentityId.FromUserAssignedClientId(options.ManagedIdentityClientId);
        tokenCredentials.Add(new ManagedIdentityCredential(managedIdentityId));

        if (!options.PreferWorkloadIdentityCredential)
        {
            AddCredential(workloadIdentityCredential);
        }

        if (!options.ExcludeAzureCliCredential)
        {
            // Add Azure CLI credential as the last resort
            // az command to disable auto update of the Azure CLI to avoid timeout waiting for
            // console input will be called before first use of AzureCliCredential
            TokenCredential azureCliCredential = new AzureCliCredentialWithAzNoUpdateWrapper(
                new AzureCliCredential(new AzureCliCredentialOptions
                {
                    ProcessTimeout = TimeSpan.FromSeconds(30)
                })
            );
            if (!options.DisableShortCache)
            {
                azureCliCredential = new TokenCredentialShortCache(azureCliCredential);
            }
            tokenCredentials.Add(azureCliCredential);
        }

        if (tokenCredentials.Count == 0)
        {
            throw new InvalidOperationException("No valid credential class detected and configured for authentication to Azure services.");
        }

        var ret = new ChainedTokenCredential(tokenCredentials.ToArray());
        return ret;

        void AddCredential(TokenCredential? credential)
        {
            if (credential == null)
            {
                return;
            }

            tokenCredentials.Add(options.DisableShortCache
                ? credential
                : new TokenCredentialShortCache(credential));
        }
    }

    private static object _workloadTokenFileLock = new object();
    private static string? _workloadTokenFile = null;
    private static string? _workloadToken = null;

    // Create WorkloadIdentityCredential if the environment variables set by AzurePipeline are provided
    private static WorkloadIdentityCredential? GetWorkloadIdentityCredentialForAzurePipelineTask()
    {
        WorkloadIdentityCredential? credential = CreateFromTokenFile(
            "HELIX_ENTRA_CLIENT_ID",
            "HELIX_ENTRA_TENANT_ID",
            "HELIX_ENTRA_TOKEN_FILE");
        if (credential != null)
        {
            return credential;
        }

        string? servicePrincipalId = Environment.GetEnvironmentVariable("servicePrincipalId");
        string? tenantId = Environment.GetEnvironmentVariable("tenantId");
        string? idToken = Environment.GetEnvironmentVariable("idToken");
        if (string.IsNullOrEmpty(servicePrincipalId) ||
            string.IsNullOrEmpty(tenantId) ||
            string.IsNullOrEmpty(idToken))
        {
            return null;
        }

        lock (_workloadTokenFileLock)
        {
            if (idToken != _workloadToken)
            {
                var tokenFileName = Path.GetTempFileName();
                File.WriteAllText(tokenFileName, idToken);
                _workloadTokenFile = tokenFileName;
                _workloadToken = idToken;
            }
            return new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions
            {
                ClientId = servicePrincipalId,
                TokenFilePath = _workloadTokenFile,
                TenantId = tenantId,
            });
        }

        static WorkloadIdentityCredential? CreateFromTokenFile(
            string clientIdVariable,
            string tenantIdVariable,
            string tokenFileVariable)
        {
            string? clientId = Environment.GetEnvironmentVariable(clientIdVariable);
            string? tenantId = Environment.GetEnvironmentVariable(tenantIdVariable);
            string? tokenFile = Environment.GetEnvironmentVariable(tokenFileVariable);
            if (string.IsNullOrEmpty(clientId) ||
                string.IsNullOrEmpty(tenantId) ||
                string.IsNullOrEmpty(tokenFile) ||
                !File.Exists(tokenFile))
            {
                return null;
            }

            return new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions
            {
                ClientId = clientId,
                TokenFilePath = tokenFile,
                TenantId = tenantId,
            });
        }
    }

    // Create AzurePipelinesCredential if the environment variables set by AzureCli task and SYSTEM_ACCESSTOKEN are provided
    private static AzurePipelinesCredential? GetAzurePipelinesCredentialForAzurePipelineTask()
    {
        string? systemAccessToken = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");
        string? clientId = Environment.GetEnvironmentVariable("AZURESUBSCRIPTION_CLIENT_ID");
        string? tenantId = Environment.GetEnvironmentVariable("AZURESUBSCRIPTION_TENANT_ID");
        string? serviceConnectionId = Environment.GetEnvironmentVariable("AZURESUBSCRIPTION_SERVICE_CONNECTION_ID");

        if (!string.IsNullOrEmpty(systemAccessToken) &&
            !string.IsNullOrEmpty(clientId) &&
            !string.IsNullOrEmpty(tenantId) &&
            !string.IsNullOrEmpty(serviceConnectionId) &&
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SYSTEM_OIDCREQUESTURI")))
        {
            return new AzurePipelinesCredential(tenantId, clientId, serviceConnectionId, systemAccessToken);
        }
        return null;
    }
}
