// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
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
            MergePolicy = (MergePolicy) (int) other.MergePolicy;
        }

        public UpdateFrequency UpdateFrequency { get; set; }

        public MergePolicy MergePolicy { get; set; }

        public Data.Models.SubscriptionPolicy ToDb()
        {
            return new Data.Models.SubscriptionPolicy
            {
                MergePolicy = (Data.Models.MergePolicy) (int) MergePolicy,
                UpdateFrequency = (Data.Models.UpdateFrequency) (int) UpdateFrequency,
            };
        }
    }
}
