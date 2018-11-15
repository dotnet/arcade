// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.DataAnnotations;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AccountParallelismItem
    {
        [Required]
        public string accountId { get; set; }
        [Required]
        public string agentCloudId { get; set; }
    }
}
