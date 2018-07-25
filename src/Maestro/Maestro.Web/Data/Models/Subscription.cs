// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JetBrains.Annotations;
using Maestro.Web.Api.v2018_07_16.Models;
using Newtonsoft.Json;

namespace Maestro.Web.Data.Models
{
    public class Subscription
    {
        public Subscription()
        {
        }

        public Subscription([NotNull] SubscriptionData other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            SourceRepository = other.SourceRepository;
            TargetRepository = other.TargetRepository;
            TargetBranch = other.TargetBranch;
            Policy = new SubscriptionPolicy(other.Policy);
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int ChannelId { get; set; }
        public Channel Channel { get; set; }

        public string SourceRepository { get; set; }

        public string TargetRepository { get; set; }

        public string TargetBranch { get; set; }

        [Column("Policy")]
        private string _Policy { get; set; }

        [NotMapped]
        public SubscriptionPolicy Policy
        {
            get => _Policy == null ? null : JsonConvert.DeserializeObject<SubscriptionPolicy>(_Policy);
            set => _Policy = value == null ? null : JsonConvert.SerializeObject(value);
        }
    }
}
