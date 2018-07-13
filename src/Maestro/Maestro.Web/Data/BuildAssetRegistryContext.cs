using Microsoft.EntityFrameworkCore;
using BuildAssetRegistryModel;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Maestro.Web.Data
{
    public class BuildAssetRegistryContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<Asset> Assets { get; set; }
        public DbSet<AssetLocation> AssetLocations { get; set; }
        public DbSet<Build> Builds { get; set; }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<SubscriptionPolicy> SubscriptionPolicies { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer("SQL-Connection-String");
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
        }
    }
}
