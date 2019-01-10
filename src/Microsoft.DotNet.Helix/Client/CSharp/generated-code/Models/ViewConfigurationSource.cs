using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class ViewConfigurationSource
    {
        public ViewConfigurationSource()
        {
        }

        [JsonProperty("repository")]
        public string Repository { get; set; }

        [JsonProperty("branch")]
        public string Branch { get; set; }

        [JsonProperty("milestone")]
        public string Milestone { get; set; }

        [JsonProperty("sortByBuild")]
        public bool? SortByBuild { get; set; }

        [JsonProperty("releaseLinks")]
        public IImmutableList<ViewConfigurationExternalTelemetry> ReleaseLinks { get; set; }

        [JsonProperty("types")]
        public IImmutableList<ViewConfigurationType> Types { get; set; }

        [JsonProperty("externalLinks")]
        public IImmutableList<ViewConfigurationExternalTelemetry> ExternalLinks { get; set; }

        [JsonProperty("buildProperties")]
        public IImmutableDictionary<string, string> BuildProperties { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }
    }
}
