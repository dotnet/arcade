using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class ContainerExtensionRequest
    {
        public ContainerExtensionRequest()
        {
        }

        [JsonProperty("ExtensionInDays")]
        public double? ExtensionInDays { get; set; }

        [JsonProperty("ContainerName")]
        public string ContainerName { get; set; }

        [JsonProperty("StorageAccountName")]
        public string StorageAccountName { get; set; }
    }
}
