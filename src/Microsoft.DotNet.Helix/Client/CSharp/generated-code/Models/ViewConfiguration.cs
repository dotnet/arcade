using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class ViewConfiguration
    {
        public ViewConfiguration()
        {
        }

        [JsonProperty("products")]
        public IImmutableList<Models.ViewConfigurationProduct> Products { get; set; }

        [JsonProperty("repositories")]
        public IImmutableList<Models.ViewConfigurationRepositories> Repositories { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }
    }
}
