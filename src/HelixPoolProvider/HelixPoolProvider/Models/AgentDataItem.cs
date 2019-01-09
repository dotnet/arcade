// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.DataAnnotations;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentDataItem
    {
        [Required]
        public string correlationId { get; set; }
        [Required]
        public string queueId { get; set; }
        [Required]
        public string workItemId { get; set; }
        [Required]
        public bool isPublicQueue { get; set; }
    }
}
