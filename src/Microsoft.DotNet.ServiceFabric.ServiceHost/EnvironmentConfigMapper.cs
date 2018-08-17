// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public class EnvironmentConfigMapper : IServiceConfig
    {
        private readonly IServiceConfig _config;
        private readonly string _env;

        public EnvironmentConfigMapper(IServiceConfig config)
        {
            _config = config;
            _env = Environment.GetEnvironmentVariable("ENVIRONMENT_NAME") ?? "Local";
        }

        public IServiceConfigSection this[string name] => new Section(_config[name], _config[$"{name}-{_env}"]);

        public class Section : IServiceConfigSection
        {
            private readonly IServiceConfigSection _baseSection;
            private readonly IServiceConfigSection _specificSection;

            public Section(IServiceConfigSection baseSection, IServiceConfigSection specificSection)
            {
                _baseSection = baseSection;
                _specificSection = specificSection;
            }

            public string this[string name] => _specificSection?[name] ?? _baseSection?[name];
        }
    }
}
