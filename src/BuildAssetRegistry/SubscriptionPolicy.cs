using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BuildAssetRegistryModel
{
    public class SubscriptionPolicy
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public UpdateFrequency UpdateFrequency { get; set; }

        public MergePolicy MergePolicy { get; set; }
    }
}
