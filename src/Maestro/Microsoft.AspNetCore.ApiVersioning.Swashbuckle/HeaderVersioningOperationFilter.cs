using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.AspNetCore.ApiVersioning.Schemes;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Microsoft.AspNetCore.ApiVersioning.Swashbuckle
{
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    internal class HeaderVersioningOperationFilter : IOperationFilter
    {
        private readonly HeaderVersioningScheme _scheme;

        public HeaderVersioningOperationFilter(HeaderVersioningScheme scheme)
        {
            _scheme = scheme;
        }

        public void Apply(Operation operation, OperationFilterContext context)
        {
            string version = context.ApiDescription.ActionDescriptor.RouteValues["version"];
            if (operation.Parameters == null)
            {
                operation.Parameters = new List<IParameter>();
            }

            operation.Parameters.Add(
                new NonBodyParameter
                {
                    In = "header",
                    Name = _scheme.HeaderName,
                    Description = "The api version",
                    Required = true,
                    Type = "string",
                    Enum = new object[] {version}
                });
        }
    }
}
