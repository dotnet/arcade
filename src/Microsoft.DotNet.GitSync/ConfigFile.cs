// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using CredentialManagement;
using log4net;
using Newtonsoft.Json;

namespace Microsoft.DotNet.GitSync
{
    internal class ConfigFile
    {
        private readonly string _path;
        private readonly ILog _logger;

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
            using (var cred = new Credential())
            {
                cred.Target = config.CredentialTarget;
                if (cred.Exists())
                {
                    cred.Load();
                    config.Password = cred.Password;
                }
                else
                {
                    throw new ArgumentException("No Github Account Linked with the Mirror");
                }
            }

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
    }
}