using JetBrains.Annotations;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Microsoft.AspNetCore.ApiVersioning.Swashbuckle
{
    [PublicAPI]
    public interface ISwaggerVersioningScheme
    {
        void Apply(Operation operation, OperationFilterContext context, string version);
    }
}
