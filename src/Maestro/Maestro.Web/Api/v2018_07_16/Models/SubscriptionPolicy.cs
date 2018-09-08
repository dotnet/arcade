// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using JetBrains.Annotations;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class SubscriptionPolicy
    {
        public SubscriptionPolicy()
        {
        }

        public SubscriptionPolicy([NotNull] Maestro.Data.Models.SubscriptionPolicy other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            UpdateFrequency = (UpdateFrequency) (int) other.UpdateFrequency;
            MergePolicies = other.MergePolicies != null
                ? other.MergePolicies.Select(p => new MergePolicy(p)).ToImmutableList()
                : ImmutableList<MergePolicy>.Empty;
        }

        [Required]
        public UpdateFrequency UpdateFrequency { get; set; }

        [Required]
        public IImmutableList<MergePolicy> MergePolicies { get; set; }

        public Data.Models.SubscriptionPolicy ToDb()
        {
            return new Data.Models.SubscriptionPolicy
            {
                MergePolicies = MergePolicies.Select(p => p.ToDb()).ToList(),
                UpdateFrequency = (Data.Models.UpdateFrequency) (int) UpdateFrequency,
            };
        }
    }
}
