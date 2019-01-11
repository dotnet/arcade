using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class JobCreationResult
    {
        public JobCreationResult(string name, string summaryUrl, string waitUrl)
        {
            Name = name;
            SummaryUrl = summaryUrl;
            WaitUrl = waitUrl;
        }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("SummaryUrl")]
        public string SummaryUrl { get; set; }

        [JsonProperty("WaitUrl")]
        public string WaitUrl { get; set; }

        [JsonIgnore]
        public bool IsValid
        {
            get
            {
                return
                    !(string.IsNullOrEmpty(Name)) &&
                    !(string.IsNullOrEmpty(SummaryUrl)) &&
                    !(string.IsNullOrEmpty(WaitUrl));
            }
        }
    }
}
