using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Microsoft.AspNetCore.ApiVersioning
{
    public interface IVersioningScheme
    {
        void Apply(SelectorModel model, string version);
    }
}
