// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Maestro.Data.Models
{
    public class Subscription
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public int ChannelId { get; set; }

        public Channel Channel { get; set; }

        public string SourceRepository { get; set; }

        public string TargetRepository { get; set; }

        public string TargetBranch { get; set; }

        [Column("Policy")]
        public string PolicyString { get; set; }

        [NotMapped]
        public SubscriptionPolicy PolicyObject
        {
            get => PolicyString == null ? null : JsonConvert.DeserializeObject<SubscriptionPolicy>(PolicyString);
            set => PolicyString = value == null ? null : JsonConvert.SerializeObject(value);
        }

        public int? LastAppliedBuildId { get; set; }
        public Build LastAppliedBuild { get; set; }
    }
}
