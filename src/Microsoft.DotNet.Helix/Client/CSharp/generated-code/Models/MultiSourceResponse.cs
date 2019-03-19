using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class MultiSourceResponse
    {
        public MultiSourceResponse(IImmutableDictionary<string, MultiSourceResponseSource> sources)
        {
            Sources = sources;
        }

        [JsonProperty("Sources")]
        public IImmutableDictionary<string, MultiSourceResponseSource> Sources { get; }
    }
}
