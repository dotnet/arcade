// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
