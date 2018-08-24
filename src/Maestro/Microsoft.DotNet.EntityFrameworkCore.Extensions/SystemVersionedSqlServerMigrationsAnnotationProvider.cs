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
        public SystemVersionedSqlServerMigrationsAnnotationProvider([NotNull] MigrationsAnnotationProviderDependencies dependencies) : base(dependencies)
        {
        }

        public override IEnumerable<IAnnotation> For(IIndex index)
        {
            foreach (var annotation in base.For(index))
            {
                yield return annotation;
            }

            var toKeep = new[]
            {
                DotNetExtensionsAnnotationNames.Columnstore
            };
            foreach (var name in toKeep)
            {
                var value = index[name];
                if (value != null)
                {
                    yield return new Annotation(name, value);
                }
            }
        }

        public override IEnumerable<IAnnotation> For(IEntityType entityType)
        {
            foreach (var annotation in base.For(entityType))
            {
                yield return annotation;
            }

            var toKeep = new[]
            {
                DotNetExtensionsAnnotationNames.HistoryTable,
                DotNetExtensionsAnnotationNames.SystemVersioned,
                DotNetExtensionsAnnotationNames.RetentionPeriod,
            };
            foreach (var name in toKeep)
            {
                var value = entityType[name];
                if (value != null)
                {
                    yield return new Annotation(name, value);
                }
            }
        }
    }
}
