// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Maestro.Web.Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maestro.Web.Data
{
    public class BuildAssetRegistryContextFactory : IDesignTimeDbContextFactory<BuildAssetRegistryContext>
    {
        public BuildAssetRegistryContext CreateDbContext(string[] args)
        {
            DbContextOptions options = new DbContextOptionsBuilder().UseSqlServer(
                    @"Data Source=(LocalDb)\MSSQLLocalDB;Initial Catalog=BuildAssetRegistry;Integrated Security=true")
                .Options;
            return new BuildAssetRegistryContext(
                new HostingEnvironment {EnvironmentName = EnvironmentName.Development},
                options);
        }
    }

    public class BuildAssetRegistryContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public BuildAssetRegistryContext(IHostingEnvironment hostingEnvironment, DbContextOptions options) : base(
            options)
        {
            HostingEnvironment = hostingEnvironment;
        }

        public IHostingEnvironment HostingEnvironment { get; }

        public DbSet<Asset> Assets { get; set; }
        public DbSet<AssetLocation> AssetLocations { get; set; }
        public DbSet<Build> Builds { get; set; }
        public DbSet<BuildChannel> BuildChannels { get; set; }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<BuildChannel>()
                .HasKey(bc => new {bc.BuildId, bc.ChannelId});

            builder.Entity<BuildChannel>()
                .HasOne(bc => bc.Build)
                .WithMany(b => b.BuildChannels)
                .HasForeignKey(bc => bc.BuildId);

            builder.Entity<BuildChannel>()
                .HasOne(bc => bc.Channel)
                .WithMany(c => c.BuildChannels)
                .HasForeignKey(bc => bc.ChannelId);

            builder.Entity<ApplicationUserPersonalAccessToken>()
                .HasIndex(t => new {t.ApplicationUserId, t.Name})
                .IsUnique();
        }
    }
}
