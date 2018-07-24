using System;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class Subscription
    {
        public Subscription(Data.Models.Subscription other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            Id = other.Id;
            Channel = other.Channel == null ? null : new Channel(other.Channel);
            SourceRepository = other.SourceRepository;
            TargetRepository = other.TargetRepository;
            TargetBranch = other.TargetBranch;
            Policy = new SubscriptionPolicy(other.Policy);
        }

        public int Id { get; }

        public Channel Channel { get; }

        public string SourceRepository { get; }

        public string TargetRepository { get; }

        public string TargetBranch { get; }

        public SubscriptionPolicy Policy { get; }
    }
}
