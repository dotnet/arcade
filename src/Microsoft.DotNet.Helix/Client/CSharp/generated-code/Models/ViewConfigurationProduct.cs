using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class ViewConfigurationProduct
    {
        public ViewConfigurationProduct()
        {
        }

        [JsonProperty("flavors")]
        public IImmutableList<Models.ViewConfigurationFlavor> Flavors { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }
    }
}
