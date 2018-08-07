// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Azure.KeyVault;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public static class KeyVaultMappedJsonConfigurationExtensions
    {
        public static IConfigurationBuilder AddKeyVaultMappedJsonFile(
            this IConfigurationBuilder builder,
            string path,
            string vaultUri,
            Func<KeyVaultClient> clientFactory)
        {
            return AddKeyVaultMappedJsonFile(builder, null, path, false, false, vaultUri, clientFactory);
        }

        public static IConfigurationBuilder AddKeyVaultMappedJsonFile(
            this IConfigurationBuilder builder,
            IFileProvider provider,
            string path,
            bool optional,
            bool reloadOnChange,
            string vaultUri,
            Func<KeyVaultClient> clientFactory)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Invalid File Path", nameof(path));
            }

            if (provider == null && Path.IsPathRooted(path))
            {
                provider = new PhysicalFileProvider(Path.GetDirectoryName(path));
                path = Path.GetFileName(path);
            }

            var source = new KeyVaultMappedJsonConfigurationSource(clientFactory, vaultUri)
            {
                FileProvider = provider,
                Path = path,
                Optional = optional,
                ReloadOnChange = reloadOnChange
            };
            builder.Add(source);
            return builder;
        }
    }
}
