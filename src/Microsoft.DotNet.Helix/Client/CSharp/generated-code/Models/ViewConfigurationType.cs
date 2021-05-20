using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class ViewConfigurationType
    {
        public ViewConfigurationType()
        {
        }

        [JsonProperty("columns")]
        public IImmutableList<Models.Displayable> Columns { get; set; }

        [JsonProperty("otherProperties")]
        public IImmutableList<Models.Displayable> OtherProperties { get; set; }

        [JsonProperty("noWorkitems")]
        public bool? NoWorkitems { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }
    }
}
