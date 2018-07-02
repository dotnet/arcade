﻿using System.Collections.Generic;

namespace Microsoft.DotNet.Helix.Client
{
    internal class JobListEntry
    {
        public string Command { get; set; }
        public List<string> CorrelationPayloadUris { get; set; } = new List<string>();
        public string PayloadUri { get; set; }
        public string WorkItemId { get; set; }
        public int TimeoutInSeconds { get; set; }
    }
}
