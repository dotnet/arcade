// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Fabric;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    internal class ServiceFabricServiceConfig : IServiceConfig
    {
        private readonly ConfigurationPackage _configPackage;

        public ServiceFabricServiceConfig(ConfigurationPackage configPackage)
        {
            _configPackage = configPackage;
        }

        public IServiceConfigSection this[string name] =>
            _configPackage.Settings.Sections.Contains(name)
                ? new ServiceFabricServiceConfigSection(_configPackage.Settings.Sections[name])
                : null;
    }
}
