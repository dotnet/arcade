// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class PullRequestJobSummary
    {
        public PullRequestJobSummary(string jobId, string status)
        {
            JobId = jobId;
            Status = status;
        }

        [JsonProperty("JobId")]
        public string JobId { get; set; }

        [JsonProperty("Status")]
        public string Status { get; set; }

        [JsonIgnore]
        public bool IsValid
        {
            get
            {
                if (string.IsNullOrEmpty(JobId))
                {
                    return false;
                }
                if (string.IsNullOrEmpty(Status))
                {
                    return false;
                }
                return true;
            }
        }
    }
}
