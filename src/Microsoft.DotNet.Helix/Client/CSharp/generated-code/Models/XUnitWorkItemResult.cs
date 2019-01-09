using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class XUnitWorkItemResult
    {
        public XUnitWorkItemResult()
        {
        }

        [JsonProperty("ResultXmlUrl")]
        public string ResultXmlUrl { get; set; }

        [JsonProperty("Results")]
        public IImmutableList<XUnitTestResult> Results { get; set; }
    }
}
