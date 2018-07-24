using System.IO;
using Microsoft.Extensions.Configuration.Json;

namespace Maestro.Web
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
