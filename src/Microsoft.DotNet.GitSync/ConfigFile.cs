// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using log4net;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;

namespace Microsoft.DotNet.GitSync
{
    internal class ConfigFile
    {
        private readonly string _path;
        private readonly ILog _logger;
        private string _clientId;
        private string _clientSecret;

        public ConfigFile(string path, ILog logger)
        {
            _path = path;
            _logger = logger;
        }

        public Configuration Get()
        {
            if (!File.Exists(_path))
            {
                return null;
            }
            var config = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(_path), new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                PreserveReferencesHandling = PreserveReferencesHandling.All,
            });

            _clientId = config.ClientId;
            _clientSecret = config.ClientSecret;

            var kvc = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(GetToken));
            config.Password = kvc.GetSecretAsync(config.SecretUri).Result.Value;

            return config;
        }

        public void Save(Configuration config)
        {
            File.WriteAllText(_path, JsonConvert.SerializeObject(config,
                Formatting.Indented,
                new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                    PreserveReferencesHandling = PreserveReferencesHandling.All
                }));
            _logger.Info("Configuration file updated");
        }

        public async Task<string> GetToken(string authority, string resource, string scope)
        {
            var authContext = new AuthenticationContext(authority);
            var clientCred = new ClientCredential(_clientId, _clientSecret);
            var result = await authContext.AcquireTokenAsync(resource, clientCred);

            if (result == null)
                throw new InvalidOperationException("Failed to obtain the github token");

            return result.AccessToken;
        }
    }
}