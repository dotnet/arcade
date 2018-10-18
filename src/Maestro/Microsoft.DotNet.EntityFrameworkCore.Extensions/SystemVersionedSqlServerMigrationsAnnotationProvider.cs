// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.SqlServer.Migrations.Internal;

namespace Microsoft.DotNet.EntityFrameworkCore.Extensions
{
    public class SystemVersionedSqlServerMigrationsAnnotationProvider : SqlServerMigrationsAnnotationProvider
    {
        public SystemVersionedSqlServerMigrationsAnnotationProvider(
            [NotNull] MigrationsAnnotationProviderDependencies dependencies) : base(dependencies)
        {
        }

        public override IEnumerable<IAnnotation> For(IIndex index)
        {
            foreach (IAnnotation annotation in base.For(index))
            {
                yield return annotation;
            }

            string[] toKeep = {DotNetExtensionsAnnotationNames.Columnstore};
            foreach (string name in toKeep)
            {
                object value = index[name];
                if (value != null)
                {
                    yield return new Annotation(name, value);
                }
            }
        }

        public override IEnumerable<IAnnotation> For(IEntityType entityType)
        {
            foreach (IAnnotation annotation in base.For(entityType))
            {
                yield return annotation;
            }

            string[] toKeep =
            {
                DotNetExtensionsAnnotationNames.HistoryTable,
                DotNetExtensionsAnnotationNames.SystemVersioned,
                DotNetExtensionsAnnotationNames.RetentionPeriod
            };
            foreach (string name in toKeep)
            {
                object value = entityType[name];
                if (value != null)
                {
                    yield return new Annotation(name, value);
                }
            }
        }
    }
}
