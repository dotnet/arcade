using System;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class SubscriptionPolicy
    {
        public SubscriptionPolicy()
        {
        }

        public SubscriptionPolicy(Data.Models.SubscriptionPolicy other)
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
    }
}
