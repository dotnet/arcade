﻿using System.Linq;
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
