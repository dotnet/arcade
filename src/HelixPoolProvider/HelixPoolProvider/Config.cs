// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.DotNet.HelixPoolProvider
{
    public class Config
    {
        private IConfiguration _configuration;
        private ILogger _logger;

        private ConcurrentDictionary<string, string> _creds = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Config(IConfiguration config, ILoggerFactory loggerFactory)
        {
            _configuration = config;
            _logger = loggerFactory.CreateLogger<Config>();
        }

        public string SharedSecret => GetSecret(_configuration[$"{nameof(SharedSecret)}-Key"]);
        public bool SharedSecretIsConfigured => TryGetSecret(_configuration[$"{nameof(SharedSecret)}-Key"], out string secretValue);
        public string ContainerName => _configuration[nameof(ContainerName)];
        public string ConnectionString => GetSecret(_configuration[$"{nameof(ConnectionString)}-Key"]);
        public bool ConnectionStringIsConfigured => TryGetSecret(_configuration[$"{nameof(ConnectionString)}-Key"], out string secretValue);
        public string ApiAuthorizationPat => GetSecret(_configuration[$"{nameof(ApiAuthorizationPat)}-Key"]);
        public bool ApiAuthorizationPatIsConfigured => TryGetSecret(_configuration[$"{nameof(ApiAuthorizationPat)}-Key"], out string secretValue);
        public AllowableHelixQueues AllowedTargetQueues => Enum.Parse<AllowableHelixQueues>(_configuration[nameof(AllowedTargetQueues)]);
        public string[] AllowedTargetQueueNames => _configuration.GetSection(nameof(AllowedTargetQueueNames)).Get<string[]>();
        public string HelixCreator => _configuration[nameof(HelixCreator)];
        public int TimeoutInMinutes => Int32.Parse(_configuration[nameof(TimeoutInMinutes)]);
        public string HelixEndpoint => _configuration[nameof(HelixEndpoint)];
        public int MaxParallelism => Int32.Parse(_configuration[nameof(MaxParallelism)]);

        public bool TryGetSecret(string secretName, out string secretValue)
        {
            try
            {
                secretValue = GetSecret(secretName);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Could not get secret {secretName}");
                secretValue = null;
                return false;
            }
        }

        public string GetSecret(string secretName)
        {
            if (string.IsNullOrEmpty(secretName))
            {
                throw new ArgumentException("Key vault secret name must be non-empty");
            }

            string currentSecret;
            if (_creds.TryGetValue(secretName, out currentSecret))
            {
                return currentSecret;
            }
            else
            {
                AzureServiceTokenProvider tokenProvider = new AzureServiceTokenProvider();
                try
                {
                    KeyVaultClient client = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(tokenProvider.KeyVaultTokenCallback));
                    var secret = client.GetSecretAsync(secretName).ConfigureAwait(false).GetAwaiter().GetResult();
                    _creds.TryAdd(secretName, secret.Value);
                    return secret.Value;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to obtain secret {secretName} from keyvault");
                    throw e;
                }
            }
        }
    }
}
