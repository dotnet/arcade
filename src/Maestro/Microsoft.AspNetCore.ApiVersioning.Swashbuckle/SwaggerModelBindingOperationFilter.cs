// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Microsoft.AspNetCore.ApiVersioning.Swashbuckle
{
    public class SwaggerModelBindingOperationFilter : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {
            foreach (IParameter parameter in operation.Parameters)
            {
                if (parameter.In.ToLower() == "modelbinding")
                {
                    parameter.In = "query";
                }

                // For some reason Swashbuckle doesn't honor ModelMetadata.IsRequired for parameters
                ApiParameterDescription apiParamDesc =
                    context.ApiDescription.ParameterDescriptions.FirstOrDefault(p => p.Name == parameter.Name);
                if (apiParamDesc != null && apiParamDesc.ModelMetadata.IsRequired)
                {
                    parameter.Required = true;
                }
            }
        }
    }
}
