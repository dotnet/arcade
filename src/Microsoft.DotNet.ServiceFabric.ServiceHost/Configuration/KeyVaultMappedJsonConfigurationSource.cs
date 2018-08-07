// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public class KeyVaultMappedJsonConfigurationSource : JsonConfigurationSource
    {
        private readonly Lazy<KeyVaultClient> _client;

        private readonly string KeyVaultKeyPrefix = "[vault(";
        private readonly string KeyVaultKeySuffix = ")]";

        public KeyVaultMappedJsonConfigurationSource(Func<KeyVaultClient> clientFactory, string vaultUri)
        {
            VaultUri = vaultUri;
            _client = new Lazy<KeyVaultClient>(clientFactory);
        }

        public string VaultUri { get; }
        private KeyVaultClient Client => _client.Value;

        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            FileProvider = FileProvider ?? builder.GetFileProvider();
            return new KeyVaultMappedJsonConfigurationProvider(this);
        }

        public IDictionary<string, string> MapKeyVaultReferences(IDictionary<string, string> data)
        {
            var returnValue = new Dictionary<string, string>();
            foreach (string key in data.Keys)
            {
                string keyVaultKey = data[key];
                if (keyVaultKey.StartsWith(KeyVaultKeyPrefix))
                {
                    keyVaultKey = keyVaultKey.Replace(KeyVaultKeyPrefix, "").Replace(KeyVaultKeySuffix, "");
                    try
                    {
                        Task<SecretBundle> t = Client.GetSecretAsync(VaultUri, keyVaultKey);
                        returnValue[key] = t.ConfigureAwait(false).GetAwaiter().GetResult().Value;
                    }
                    catch (KeyVaultErrorException)
                    {
                        returnValue[key] = data[key];
                    }
                }
                else
                {
                    returnValue[key] = data[key];
                }
            }

            return returnValue;
        }
    }
}
