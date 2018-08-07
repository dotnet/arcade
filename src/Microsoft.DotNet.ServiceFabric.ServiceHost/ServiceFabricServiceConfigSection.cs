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