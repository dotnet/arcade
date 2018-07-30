// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Fabric;
using System.Fabric.Description;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    internal class ServiceFabricServiceContext : IServiceContext
    {
        private readonly ServiceContext _context;

        public ServiceFabricServiceContext(ServiceContext context)
        {
            _context = context;
        }

        public IServiceConfig Config =>
            new ServiceFabricServiceConfig(_context.CodePackageActivationContext.GetConfigurationPackageObject("Config"));

        public bool IsServiceFabric => true;
    }

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

    internal class ServiceFabricServiceConfigSection : IServiceConfigSection
    {
        private readonly ConfigurationSection _section;

        public ServiceFabricServiceConfigSection(ConfigurationSection section)
        {
            _section = section;
        }

        public string this[string name]
        {
            get {
                if (_section.Parameters.Contains(name))
                {
                    return _section.Parameters[name].Value;
                }
                else
                    return null;
            }
        }
    }
}
