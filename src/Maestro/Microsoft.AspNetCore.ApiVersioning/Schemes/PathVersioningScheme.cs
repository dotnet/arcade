using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Microsoft.AspNetCore.ApiVersioning.Schemes
{
    public class PathVersioningScheme : IVersioningScheme
    {
        public void Apply(SelectorModel model, string version)
        {
            AttributeRouteModel attributeRouteModel = model.AttributeRouteModel;
            attributeRouteModel.Template = $"api/{version}/{attributeRouteModel.Template}";
        }
    }
}
