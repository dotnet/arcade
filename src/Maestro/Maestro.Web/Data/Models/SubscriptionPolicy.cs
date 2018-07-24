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
