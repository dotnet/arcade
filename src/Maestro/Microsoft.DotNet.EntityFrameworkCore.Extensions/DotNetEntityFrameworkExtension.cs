// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.EntityFrameworkCore.Extensions
{
    public class DotNetEntityFrameworkExtension : IDbContextOptionsExtension
    {
        public bool ApplyServices(IServiceCollection services)
        {
            services
                .AddSingleton<IMigrationsAnnotationProvider, SystemVersionedSqlServerMigrationsAnnotationProvider>();
            services.AddSingleton<IMigrationsSqlGenerator, SystemVersionedSqlServerMigrationsSqlGenerator>();
            return false;
        }

        public long GetServiceProviderHashCode()
        {
            return 0;
        }

        public void Validate(IDbContextOptions options)
        {
        }

        public string LogFragment => "SystemVersioning Enabled";
    }
}
