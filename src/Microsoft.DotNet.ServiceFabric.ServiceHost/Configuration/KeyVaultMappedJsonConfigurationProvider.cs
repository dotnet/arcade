// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.Extensions.Configuration.Json;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public class KeyVaultMappedJsonConfigurationProvider : JsonConfigurationProvider
    {
        public KeyVaultMappedJsonConfigurationProvider(KeyVaultMappedJsonConfigurationSource source) : base(source)
        {
        }

        public override void Load(Stream stream)
        {
            base.Load(stream);
            Data = ((KeyVaultMappedJsonConfigurationSource) Source).MapKeyVaultReferences(Data);
        }
    }
}
