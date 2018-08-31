using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.ApiPagination
{
    [PublicAPI]
    public static class ApiPaginationMvcBuilderExtensions
    {
        public static IMvcBuilder AddApiPagination(this IMvcBuilder builder)
        {
            return builder.AddMvcOptions(ConfigureMvcOptions);
        }

        private static void ConfigureMvcOptions(MvcOptions options)
        {
            options.Conventions.Add(new ApiPaginationApplicationModelConvention());
        }

        public static IMvcCoreBuilder AddApiPagination(this IMvcCoreBuilder builder)
        {
            return builder.AddMvcOptions(ConfigureMvcOptions);
        }
    }
}
