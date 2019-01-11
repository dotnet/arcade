using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class XUnitTestResult
    {
        public XUnitTestResult()
        {
        }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("Result")]
        public string Result { get; set; }

        [JsonProperty("FailureExceptionType")]
        public string FailureExceptionType { get; set; }

        [JsonProperty("FailureMessage")]
        public string FailureMessage { get; set; }

        [JsonProperty("FailureStackTrace")]
        public string FailureStackTrace { get; set; }

        [JsonProperty("Reason")]
        public string Reason { get; set; }

        [JsonProperty("Duration")]
        public long? Duration { get; set; }

        [JsonProperty("Output")]
        public string Output { get; set; }

        [JsonProperty("FailureReason")]
        public FailureReason FailureReason { get; set; }

        [JsonProperty("Type")]
        public string Type { get; set; }

        [JsonProperty("Method")]
        public string Method { get; set; }

        [JsonProperty("Arguments")]
        public string Arguments { get; set; }
    }
}
