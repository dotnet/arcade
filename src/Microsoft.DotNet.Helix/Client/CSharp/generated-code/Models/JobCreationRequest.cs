using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class JobCreationRequest
    {
        public JobCreationRequest(string source, string type, string build, IImmutableDictionary<string, string> properties, string listUri, string queueId)
        {
            Source = source;
            Type = type;
            Build = build;
            Properties = properties;
            ListUri = listUri;
            QueueId = queueId;
        }

        [JsonProperty("Source")]
        public string Source { get; set; }

        [JsonProperty("Type")]
        public string Type { get; set; }

        [JsonProperty("Build")]
        public string Build { get; set; }

        [JsonProperty("Properties")]
        public IImmutableDictionary<string, string> Properties { get; set; }

        [JsonProperty("ListUri")]
        public string ListUri { get; set; }

        [JsonProperty("QueueId")]
        public string QueueId { get; set; }

        [JsonProperty("ResultsUri")]
        public string ResultsUri { get; set; }

        [JsonProperty("ResultsUriRSAS")]
        public string ResultsUriRSAS { get; set; }

        [JsonProperty("ResultsUriWSAS")]
        public string ResultsUriWSAS { get; set; }

        [JsonProperty("Creator")]
        public string Creator { get; set; }

        [JsonProperty("Branch")]
        public string Branch { get; set; }

        [JsonProperty("Repository")]
        public string Repository { get; set; }

        [JsonProperty("PullRequestId")]
        public string PullRequestId { get; set; }

        [JsonProperty("MaxRetryCount")]
        public int? MaxRetryCount { get; set; }

        [JsonProperty("Attempt")]
        public string Attempt { get; set; }

        [JsonProperty("JobStartIdentifier")]
        public string JobStartIdentifier { get; set; }

        [JsonIgnore]
        public bool IsValid
        {
            get
            {
                if (string.IsNullOrEmpty(Source))
                {
                    return false;
                }
                if (string.IsNullOrEmpty(Type))
                {
                    return false;
                }
                if (string.IsNullOrEmpty(Build))
                {
                    return false;
                }
                if (Properties == default)
                {
                    return false;
                }
                if (string.IsNullOrEmpty(ListUri))
                {
                    return false;
                }
                if (string.IsNullOrEmpty(QueueId))
                {
                    return false;
                }
                return true;
            }
        }
    }
}
