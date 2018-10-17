// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using JetBrains.Annotations;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class Subscription
    {
        public Subscription([NotNull] Data.Models.Subscription other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            Id = other.Id;
            Channel = other.Channel == null ? null : new Channel(other.Channel);
            LastAppliedBuild = other.LastAppliedBuild == null ? null : new Build(other.LastAppliedBuild);
            SourceRepository = other.SourceRepository;
            TargetRepository = other.TargetRepository;
            TargetBranch = other.TargetBranch;
            Enabled = other.Enabled;
            Policy = new SubscriptionPolicy(other.PolicyObject);
        }

        public Guid Id { get; }

        public Channel Channel { get; }

        public string SourceRepository { get; }

        public string TargetRepository { get; }

        public string TargetBranch { get; }

        public SubscriptionPolicy Policy { get; }

        public Build LastAppliedBuild { get; }

        public bool Enabled { get; }
    }
}
