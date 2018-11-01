// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.DataAnnotations;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class SubscriptionData
    {
        [Required]
        public string ChannelName { get; set; }

        [Required]
        public string SourceRepository { get; set; }

        [Required]
        [RepositoryUrl]
        public string TargetRepository { get; set; }

        [Required]
        public string TargetBranch { get; set; }

        public bool? Enabled { get; set; }

        [Required]
        public SubscriptionPolicy Policy { get; set; }

        public Data.Models.Subscription ToDb()
        {
            return new Data.Models.Subscription
            {
                SourceRepository = SourceRepository,
                TargetRepository = TargetRepository,
                TargetBranch = TargetBranch,
                PolicyObject = Policy.ToDb(),
                Enabled = Enabled ?? true
            };
        }
    }
}
