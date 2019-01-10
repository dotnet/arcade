using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class AnalysisReport
    {
        public AnalysisReport()
        {
        }

        [JsonProperty("xunit")]
        public XUnitWorkItemResult Xunit { get; set; }

        [JsonProperty("external")]
        public ExternalLinkResult External { get; set; }
    }
}
