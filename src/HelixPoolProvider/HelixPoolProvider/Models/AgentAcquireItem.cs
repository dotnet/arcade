// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.DataAnnotations;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentAcquireItem
    {
        [Required]
        public string agentId { get; set; }
        [Required]
        public string agentPool { get; set; }
        [Required]
        public string accountId { get; set; }
        public string failRequestUrl { get; set; }
        public string appendRequestMessageUrl { get; set; }
        [Required]
        public AgentConfigurationItem agentConfiguration { get; set; }
        [Required]
        public object agentSpecification { get; set; }
    }
}
