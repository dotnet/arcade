// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentReleaseItem
    {
        public string agentId { get; set; }
        public string accountId { get; set; }
        public string agentCloudId { get; set; }
        public string agentPool { get; set; }
        public AgentDataItem agentData { get; set; }
    }
}
