using System.ComponentModel.DataAnnotations;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class SubscriptionData
    {
        [Required]
        public string ChannelName { get; set; }

        [Required]
        public string SourceRepository { get; set; }

        [Required]
        public string TargetRepository { get; set; }

        [Required]
        public string TargetBranch { get; set; }

        [Required]
        public SubscriptionPolicy Policy { get; set; }
    }
}
