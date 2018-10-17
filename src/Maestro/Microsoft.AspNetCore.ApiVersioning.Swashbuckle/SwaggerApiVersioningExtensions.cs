// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Microsoft.AspNetCore.ApiVersioning.Swashbuckle
{
    [PublicAPI]
    public static class SwaggerApiVersioningExtensions
    {
        public static IServiceCollection AddSwaggerApiVersioning(this IServiceCollection services)
        {
            return services.AddSingleton<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerVersions>();
        }

        public static void FilterOperations(
            this SwaggerGenOptions options,
            Action<Operation, OperationFilterContext> filter)
        {
            options.OperationFilter<FunctionOperationFilter>(filter);
        }

        public static void FilterDocument(
            this SwaggerGenOptions options,
            Action<SwaggerDocument, DocumentFilterContext> filter)
        {
            options.DocumentFilter<FunctionDocumentFilter>(filter);
        }


        private class FunctionOperationFilter : IOperationFilter
        {
            public FunctionOperationFilter(Action<Operation, OperationFilterContext> filter)
            {
                Filter = filter;
            }

            public Action<Operation, OperationFilterContext> Filter { get; }

            public void Apply(Operation operation, OperationFilterContext context)
            {
                Filter(operation, context);
            }
        }

        private class FunctionDocumentFilter : IDocumentFilter
        {
            public FunctionDocumentFilter(Action<SwaggerDocument, DocumentFilterContext> filter)
            {
                Filter = filter;
            }

            public Action<SwaggerDocument, DocumentFilterContext> Filter { get; }

            public void Apply(SwaggerDocument model, DocumentFilterContext context)
            {
                Filter(model, context);
            }
        }
    }
}
