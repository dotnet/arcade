// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Fabric.Description;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    internal class ServiceFabricServiceConfigSection : IServiceConfigSection
    {
        private readonly ConfigurationSection _section;

        public ServiceFabricServiceConfigSection(ConfigurationSection section)
        {
            _section = section;
        }

        public string this[string name]
        {
            get
            {
                if (_section.Parameters.Contains(name))
                {
                    return _section.Parameters[name].Value;
                }

                return null;
            }
        }
    }
}
