using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class ContainerCreationRequest
    {
        public ContainerCreationRequest()
        {
        }

        [JsonProperty("ExpirationInDays")]
        public double ExpirationInDays { get; set; }

        [JsonProperty("DesiredName")]
        public string DesiredName { get; set; }
    }
}
