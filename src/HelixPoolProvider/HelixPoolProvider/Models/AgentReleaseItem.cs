// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.DataAnnotations;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentReleaseItem
    {
        [Required]
        public string agentId { get; set; }
        public string accountId { get; set; }
        public string agentPool { get; set; }
        public AgentDataItem agentData { get; set; }
    }
}
