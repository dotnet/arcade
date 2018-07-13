using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BuildAssetRegistryModel
{
    public class Build
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Repository { get; set; }

        public string Commit { get; set; }

        public string BuildNumber { get; set; }

        public DateTimeOffset DateProduced { get; set; }

        public List<Channel> Channels { get; set; }

        public List<Asset> Assets { get; set; }

        [ForeignKey("DependencyBuildId")]
        public List<Build> Dependencies { get; set; }

        public override bool Equals(object obj)
        {
            return !(obj is Build b)
                ? false
                : ((Id != 0 ? b.Id == Id : true)
                && (!string.IsNullOrEmpty(Repository) ? EF.Functions.Like(b.Repository, Repository) : true)
                && (!string.IsNullOrEmpty(Commit) ? EF.Functions.Like(b.Commit, Commit) : true)
                && (!string.IsNullOrEmpty(BuildNumber) ? EF.Functions.Like(b.Repository, BuildNumber) : true)
                && (DateProduced != default(DateTimeOffset) ? b.DateProduced == DateProduced : true));
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
