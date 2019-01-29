using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class ViewConfigurationFlavor
    {
        public ViewConfigurationFlavor()
        {
        }

        [JsonProperty("sources")]
        public IImmutableList<ViewConfigurationSource> Sources { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }
    }
}
