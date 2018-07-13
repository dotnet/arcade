using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BuildAssetRegistryModel
{
    public class Channel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Name { get; set; }

        public string Classification { get; set; }

        public override bool Equals(object obj)
        {
            return !(obj is Channel channel)
                ? false
                : EF.Functions.Like(Name, channel.Name) &&
                EF.Functions.Like(Classification, channel.Classification);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
