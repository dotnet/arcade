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

        public long GetServiceProviderHashCode() => 0;

        public void Validate(IDbContextOptions options)
        {
        }

        public string LogFragment => "SystemVersioning Enabled";
    }
}
