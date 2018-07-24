using JetBrains.Annotations;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Microsoft.AspNetCore.ApiVersioning.Swashbuckle
{
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    internal class VersioningOperationFilter : IOperationFilter
    {
        private readonly ISwaggerVersioningScheme _scheme;

        public VersioningOperationFilter(ISwaggerVersioningScheme scheme)
        {
            _scheme = scheme;
        }

        public void Apply(Operation operation, OperationFilterContext context)
        {
            string version = context.ApiDescription.ActionDescriptor.RouteValues["version"];
            _scheme.Apply(operation, context, version);
        }
    }
}
