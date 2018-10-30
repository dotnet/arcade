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
using Microsoft.DotNet.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Query;
using Newtonsoft.Json.Linq;

namespace Maestro.Data
{
    public class BuildAssetRegistryContextFactory : IDesignTimeDbContextFactory<BuildAssetRegistryContext>
    {
        public BuildAssetRegistryContext CreateDbContext(string[] args)
        {
            var connectionString =
                @"Data Source=localhost\SQLEXPRESS;Initial Catalog=BuildAssetRegistry;Integrated Security=true";

            var envVarConnectionString = Environment.GetEnvironmentVariable("BUILD_ASSET_REGISTRY_DB_CONNECTION_STRING");
            if (!string.IsNullOrEmpty(envVarConnectionString))
            {
                Console.WriteLine("Using Connection String from environment.");
                connectionString = envVarConnectionString;
            }

            DbContextOptions options = new DbContextOptionsBuilder()
                .UseSqlServer(connectionString)
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
        public DbSet<SubscriptionUpdate> SubscriptionUpdates { get; set; }
        public DbSet<Repository> Repositories { get; set; }
        public DbSet<RepositoryBranch> RepositoryBranches { get; set; }
        public DbSet<RepositoryBranchUpdate> RepositoryBranchUpdates { get; set; }
        public DbQuery<RepositoryBranchUpdateHistoryEntry> RepositoryBranchUpdateHistory { get; set; }
        public DbQuery<SubscriptionUpdateHistoryEntry> SubscriptionUpdateHistory { get; set; }


        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return this.SaveChangesWithTriggersAsync(
                base.SaveChangesAsync,
                acceptAllChangesOnSuccess,
                cancellationToken);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddDotNetExtensions();
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Channel>().HasIndex(c => c.Name).IsUnique();

            builder.Entity<BuildChannel>()
            .HasKey(
                bc => new
                {
                    bc.BuildId,
                    bc.ChannelId
                });

            builder.Entity<BuildChannel>()
                .HasOne(bc => bc.Build)
                .WithMany(b => b.BuildChannels)
                .HasForeignKey(bc => bc.BuildId);

            builder.Entity<BuildChannel>()
                .HasOne(bc => bc.Channel)
                .WithMany(c => c.BuildChannels)
                .HasForeignKey(bc => bc.ChannelId);

            builder.Entity<ApplicationUserPersonalAccessToken>()
                .HasIndex(
                    t => new
                    {
                        t.ApplicationUserId,
                        t.Name
                    })
                .IsUnique();

            builder.Entity<DefaultChannel>()
                .HasIndex(
                    dc => new
                    {
                        dc.Repository,
                        dc.Branch,
                        dc.ChannelId
                    })
                .IsUnique();

            builder.Entity<SubscriptionUpdate>()
                .HasOne(su => su.Subscription)
                .WithOne()
                .HasForeignKey<SubscriptionUpdate>(su => su.SubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.ForSqlServerIsSystemVersioned<SubscriptionUpdate, SubscriptionUpdateHistory>("1 MONTHS");

            builder.Entity<SubscriptionUpdateHistory>().HasIndex("SubscriptionId", "SysEndTime", "SysStartTime");

            builder.Entity<Repository>().HasKey(r => new {r.RepositoryName});

            builder.Entity<RepositoryBranch>()
            .HasKey(
                rb => new
                {
                    rb.RepositoryName,
                    rb.BranchName
                });

            builder.Entity<RepositoryBranch>()
                .HasOne(rb => rb.Repository)
                .WithMany(r => r.Branches)
                .HasForeignKey(rb => new {rb.RepositoryName});

            builder.Entity<RepositoryBranchUpdate>()
            .HasKey(
                ru => new
                {
                    ru.RepositoryName,
                    ru.BranchName
                });

            builder.Entity<RepositoryBranchUpdate>()
                .HasOne(ru => ru.RepositoryBranch)
                .WithOne()
                .HasForeignKey<RepositoryBranchUpdate>(
                    ru => new
                    {
                        ru.RepositoryName,
                        ru.BranchName
                    })
                .OnDelete(DeleteBehavior.Restrict);

            builder.ForSqlServerIsSystemVersioned<RepositoryBranchUpdate, RepositoryBranchUpdateHistory>("3 MONTHS");

            builder.Entity<RepositoryBranchUpdateHistory>()
            .HasKey(
                ru => new
                {
                    ru.RepositoryName,
                    ru.BranchName
                });

            builder.Entity<RepositoryBranchUpdateHistory>()
                .HasIndex("RepositoryName", "BranchName", "SysEndTime", "SysStartTime");

            builder.HasDbFunction(() => JsonExtensions.JsonValue("", "")).HasName("JSON_VALUE").HasSchema("");

            builder.Query<SubscriptionUpdateHistoryEntry>()
                .ToQuery(
                    () => SubscriptionUpdates.FromSql(
                            @"
SELECT * FROM [SubscriptionUpdates]
FOR SYSTEM_TIME ALL
")
                        .Select(
                            u => new SubscriptionUpdateHistoryEntry
                            {
                                SubscriptionId = u.SubscriptionId,
                                Action = u.Action,
                                Success = u.Success,
                                ErrorMessage = u.ErrorMessage,
                                Method = u.Method,
                                Arguments = u.Arguments,
                                Timestamp = EF.Property<DateTime>(u, "SysStartTime")
                            }));

            builder.Query<RepositoryBranchUpdateHistoryEntry>()
                .ToQuery(
                    () => RepositoryBranchUpdates.FromSql(
                            @"
SELECT * FROM [RepositoryBranchUpdates]
FOR SYSTEM_TIME ALL
")
                        .Select(
                            u => new RepositoryBranchUpdateHistoryEntry
                            {
                                Repository = u.RepositoryName,
                                Branch = u.BranchName,
                                Action = u.Action,
                                Success = u.Success,
                                ErrorMessage = u.ErrorMessage,
                                Method = u.Method,
                                Arguments = u.Arguments,
                                Timestamp = EF.Property<DateTime>(u, "SysStartTime")
                            }));
        }

        public Task<long> GetInstallationId(string repositoryUrl)
        {
            return Repositories.Where(r => r.RepositoryName == repositoryUrl)
                .Select(r => r.InstallationId)
                .FirstOrDefaultAsync();
        }
    }

    public static class JsonExtensions
    {
        public static string JsonValue(string column, [NotParameterized] string path)
        {
            // The Entity Framework in memory provider will call this so it needs to be implemented
            var lax = true;
            if (path.StartsWith("lax "))
            {
                path = path.Substring("lax ".Length);
            }
            else if (path.StartsWith("strict "))
            {
                lax = false;
                path = path.Substring("strict ".Length);
            }

            JToken token = JObject.Parse(column).SelectToken(path, !lax);
            return token.ToObject<string>();
        }
    }

    public class SubscriptionUpdateHistoryEntry
    {
        public Guid SubscriptionId { get; set; }
        public string Action { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string Method { get; set; }
        public string Arguments { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class RepositoryBranchUpdateHistoryEntry
    {
        public string Repository { get; set; }
        public string Branch { get; set; }
        public string Action { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string Method { get; set; }
        public string Arguments { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
