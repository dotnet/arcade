namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class SubscriptionUpdate
    {
        public string ChannelName { get; set; }
        public string SourceRepository { get; set; }
        public SubscriptionPolicy Policy { get; set; }
    }
}