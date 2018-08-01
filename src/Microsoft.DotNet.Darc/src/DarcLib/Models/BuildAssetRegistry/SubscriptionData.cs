namespace Microsoft.DotNet.DarcLib
{
    public class SubscriptionData
    {
        public string ChannelName { get; set; }

        public string SourceRepository { get; set; }

        public string TargetRepository { get; set; }

        public string TargetBranch { get; set; }

        public SubscriptionPolicy Policy { get; set; }
    }
}
