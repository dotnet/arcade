// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
namespace Maestro.Web.Data.Models
{
    public class SubscriptionPolicy
    {
        public SubscriptionPolicy()
        {
        }

        public SubscriptionPolicy(Api.v2018_07_16.Models.SubscriptionPolicy other)
        {
            UpdateFrequency = (UpdateFrequency) (int) other.UpdateFrequency;
            MergePolicy = (MergePolicy) (int) other.MergePolicy;
        }

        public UpdateFrequency UpdateFrequency { get; set; }

        public MergePolicy MergePolicy { get; set; }
    }
}
