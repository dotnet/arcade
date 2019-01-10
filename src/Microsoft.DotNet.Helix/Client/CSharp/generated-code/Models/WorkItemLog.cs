using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class WorkItemLog
    {
        public WorkItemLog(string module, string uri)
        {
            Module = module;
            Uri = uri;
        }

        [JsonProperty("Module")]
        public string Module { get; set; }

        [JsonProperty("Uri")]
        public string Uri { get; set; }

        [JsonIgnore]
        public bool IsValid
        {
            get
            {
                return
                    !(string.IsNullOrEmpty(Module)) &&
                    !(string.IsNullOrEmpty(Uri));
            }
        }
    }
}
