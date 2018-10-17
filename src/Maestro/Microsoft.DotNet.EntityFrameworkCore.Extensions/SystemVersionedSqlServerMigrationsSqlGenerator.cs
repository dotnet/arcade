// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.SqlServer.Metadata.Internal;

namespace Microsoft.DotNet.EntityFrameworkCore.Extensions
{
    public class SystemVersionedSqlServerMigrationsSqlGenerator : SqlServerMigrationsSqlGenerator
    {
        public SystemVersionedSqlServerMigrationsSqlGenerator(
            [NotNull] MigrationsSqlGeneratorDependencies dependencies,
            [NotNull] IMigrationsAnnotationProvider migrationsAnnotations) : base(dependencies, migrationsAnnotations)
        {
        }

        public override IReadOnlyList<MigrationCommand> Generate(
            IReadOnlyList<MigrationOperation> operations,
            IModel model)
        {
            // A history table must have its indexes defined before it is referenced in the 
            // CREATE TABLE statement for the System Versioned table
            // Move all Indexes for the history table to right before said history table
            List<MigrationOperation> toSort = operations.ToList();
            List<MigrationOperation> historyTables = toSort.Where(IsHistoryTable).ToList();
            foreach (MigrationOperation table in historyTables)
            {
                int insertIdx = toSort.IndexOf(table) + 1;
                int idx = insertIdx;
                while (idx < toSort.Count)
                {
                    MigrationOperation op = toSort[idx];
                    if (IsIndexForTable(op, table))
                    {
                        toSort.RemoveAt(idx);
                        toSort.Insert(insertIdx, op);
                        insertIdx++;
                    }

                    idx++;
                }
            }

            return base.Generate(toSort, model);
        }

        private bool IsHistoryTable(MigrationOperation op)
        {
            if (op is CreateTableOperation cto && cto[DotNetExtensionsAnnotationNames.HistoryTable] != null)
            {
                return true;
            }

            return false;
        }

        private bool IsIndexForTable(MigrationOperation op, MigrationOperation table)
        {
            if (table is CreateTableOperation createTableOperation && op is CreateIndexOperation createIndexOperation)
            {
                return createIndexOperation.Table == createTableOperation.Name;
            }

            return false;
        }

        protected override void Generate(
            CreateTableOperation operation,
            IModel model,
            MigrationCommandListBuilder builder)
        {
            Generate(operation, model, builder, false);

            bool memoryOptimized = operation[SqlServerAnnotationNames.MemoryOptimized] as bool? == true;
            var historyEntityTypeName = operation[DotNetExtensionsAnnotationNames.SystemVersioned] as string;
            var tableOptions = new List<string>();
            if (memoryOptimized)
            {
                tableOptions.Add("MEMORY_OPTIMIZED = ON");
            }

            if (historyEntityTypeName != null)
            {
                IEntityType historyEntityType = model.FindEntityType(historyEntityTypeName);
                var versioningOptions = new List<string>
                {
                    $"HISTORY_TABLE = {Dependencies.SqlGenerationHelper.DelimitIdentifier(historyEntityType[RelationalAnnotationNames.TableName] as string, operation.Schema ?? "dbo")}"
                };
                var retentionPeriod = operation[DotNetExtensionsAnnotationNames.RetentionPeriod] as string;
                if (retentionPeriod != null)
                {
                    versioningOptions.Add($"HISTORY_RETENTION_PERIOD = {retentionPeriod}");
                }

                tableOptions.Add($"SYSTEM_VERSIONING = ON ({string.Join(", ", versioningOptions)})");
            }

            if (tableOptions.Any())
            {
                builder.AppendLine();
                using (builder.Indent())
                {
                    builder.AppendLine("WITH (");
                    using (builder.Indent())
                    {
                        for (var i = 0; i < tableOptions.Count; i++)
                        {
                            string option = tableOptions[i];
                            builder.Append(option);
                            if (i + 1 != tableOptions.Count)
                            {
                                builder.AppendLine(",");
                            }
                            else
                            {
                                builder.AppendLine();
                            }
                        }
                    }

                    builder.Append(")");
                }
            }

            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator).EndCommand(memoryOptimized);
        }

        protected override void Generate(
            [NotNull] CreateTableOperation operation,
            [CanBeNull] IModel model,
            [NotNull] MigrationCommandListBuilder builder,
            bool terminate)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            bool systemVersioned =
                !string.IsNullOrEmpty(operation[DotNetExtensionsAnnotationNames.SystemVersioned] as string);
            bool historyTable = operation[DotNetExtensionsAnnotationNames.HistoryTable] as bool? == true;

            builder.Append("CREATE TABLE ")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name, operation.Schema))
                .AppendLine(" (");

            using (builder.Indent())
            {
                for (var i = 0; i < operation.Columns.Count; i++)
                {
                    AddColumnOperation column = operation.Columns[i];
                    ColumnDefinition(column, model, builder);

                    if (i != operation.Columns.Count - 1)
                    {
                        builder.AppendLine(",");
                    }
                }

                if (systemVersioned)
                {
                    builder.AppendLine(",");
                    builder.Append("PERIOD FOR SYSTEM_TIME (SysStartTime,SysEndTime)");
                }

                if (operation.PrimaryKey != null && !historyTable)
                {
                    builder.AppendLine(",");
                    PrimaryKeyConstraint(operation.PrimaryKey, model, builder);
                }

                foreach (AddUniqueConstraintOperation uniqueConstraint in operation.UniqueConstraints)
                {
                    builder.AppendLine(",");
                    UniqueConstraint(uniqueConstraint, model, builder);
                }

                foreach (AddForeignKeyOperation foreignKey in operation.ForeignKeys)
                {
                    builder.AppendLine(",");
                    ForeignKeyConstraint(foreignKey, model, builder);
                }

                builder.AppendLine();
            }

            builder.Append(")");

            if (terminate)
            {
                builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
                EndStatement(builder);
            }
        }

        protected override void Generate(
            CreateIndexOperation operation,
            IModel model,
            MigrationCommandListBuilder builder,
            bool terminate)
        {
            // clustered columnstore indexes cannot define columns
            bool columnstore = operation[DotNetExtensionsAnnotationNames.Columnstore] as bool? == true;
            bool clustered = operation[SqlServerAnnotationNames.Clustered] as bool? == true;
            if (clustered && columnstore)
            {
                builder.Append("CREATE ");
                IndexTraits(operation, model, builder);
                builder.Append("INDEX ")
                    .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
                    .Append(" ON ")
                    .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema));
                if (terminate)
                {
                    builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
                    EndStatement(builder);
                }

                return;
            }

            base.Generate(operation, model, builder, terminate);
        }

        protected override void IndexTraits(
            MigrationOperation operation,
            IModel model,
            MigrationCommandListBuilder builder)
        {
            base.IndexTraits(operation, model, builder);
            bool columnstore = operation[DotNetExtensionsAnnotationNames.Columnstore] as bool? == true;
            if (columnstore)
            {
                builder.Append("COLUMNSTORE ");
            }
        }
    }
}
