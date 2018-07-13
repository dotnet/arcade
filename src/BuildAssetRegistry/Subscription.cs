using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BuildAssetRegistryModel
{
    public class Subscription
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string ChannelName { get; set; }

        public string SourceRepository { get; set; }

        public string TargetRepository { get; set; }

        public string TargetBranch { get; set; }

        public SubscriptionPolicy Policy { get; set; }
    }
}
