// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.DotNet.EntityFrameworkCore.Extensions
{
    public static class SystemVersionedSqlServerEntityTypeBuilderExtensions
    {
        public static ModelBuilder ForSqlServerIsSystemVersioned<TEntity, THistoryEntity>(
            [NotNull] this ModelBuilder modelBuilder,
            string retentionPeriod) where TEntity : class where THistoryEntity : class
        {
            EntityTypeBuilder<THistoryEntity> historyBuilder = modelBuilder.Entity<THistoryEntity>();
            EntityTypeBuilder<TEntity> tableBuilder = modelBuilder.Entity<TEntity>();
            historyBuilder.Metadata.SetAnnotation(DotNetExtensionsAnnotationNames.HistoryTable, true);
            tableBuilder.Metadata.SetAnnotation(
                DotNetExtensionsAnnotationNames.SystemVersioned,
                historyBuilder.Metadata.Name);
            tableBuilder.Metadata.SetAnnotation(DotNetExtensionsAnnotationNames.RetentionPeriod, retentionPeriod);

            tableBuilder.Property<DateTime>("SysStartTime")
                .HasColumnType("datetime2 GENERATED ALWAYS AS ROW START")
                .ValueGeneratedOnAddOrUpdate();
            tableBuilder.Property<DateTime>("SysEndTime")
                .HasColumnType("datetime2 GENERATED ALWAYS AS ROW END")
                .ValueGeneratedOnAddOrUpdate();

            historyBuilder.Property<DateTime>("SysStartTime").HasColumnType("datetime2").ValueGeneratedOnAddOrUpdate();
            historyBuilder.Property<DateTime>("SysEndTime").HasColumnType("datetime2").ValueGeneratedOnAddOrUpdate();

            historyBuilder.HasIndex("SysEndTime", "SysStartTime").ForSqlServerIsClustered();

            return modelBuilder;
        }

        public static IndexBuilder ForSqlServerIsColumnstore([NotNull] this IndexBuilder indexBuilder)
        {
            indexBuilder.Metadata.SetAnnotation(DotNetExtensionsAnnotationNames.Columnstore, true);
            return indexBuilder;
        }

        public static DbContextOptionsBuilder AddDotNetExtensions(this DbContextOptionsBuilder optionsBuilder)
        {
            ((IDbContextOptionsBuilderInfrastructure) optionsBuilder).AddOrUpdateExtension(
                new DotNetEntityFrameworkExtension());
            return optionsBuilder;
        }
    }
}
