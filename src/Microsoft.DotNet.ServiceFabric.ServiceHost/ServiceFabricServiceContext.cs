// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Fabric;

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
            new ServiceFabricServiceConfig(
                _context.CodePackageActivationContext.GetConfigurationPackageObject("Config"));

        public bool IsServiceFabric => true;
    }
}
