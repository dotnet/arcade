// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EntityFrameworkCore.Triggers;
using Maestro.Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Query;

namespace Maestro.Data
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
        public DbSet<DefaultChannel> DefaultChannels { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<RepoInstallation> RepoInstallations { get; set; }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = new CancellationToken())
        {
            return this.SaveChangesWithTriggersAsync(base.SaveChangesAsync, acceptAllChangesOnSuccess, cancellationToken);
        }

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

            builder.Entity<DefaultChannel>()
                .HasIndex(dc => new {dc.Repository, dc.Branch})
                .IsUnique();

            builder.HasDbFunction(() => JsonExtensions.JsonValue("", ""))
                .HasName("JSON_VALUE")
                .HasSchema("");
        }

        public Task<long> GetInstallationId(string repositoryUrl)
        {
            return RepoInstallations
                .Where(ri => ri.Repository == repositoryUrl)
                .Select(ri => ri.InstallationId)
                .FirstAsync();
        }
    }

    public static class JsonExtensions
    {
        public static string JsonValue(string column, [NotParameterized] string path)
        {
            throw new NotSupportedException();
        }
    }
}
