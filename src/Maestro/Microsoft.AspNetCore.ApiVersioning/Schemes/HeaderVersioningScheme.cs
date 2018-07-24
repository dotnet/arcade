using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Microsoft.AspNetCore.ApiVersioning.Schemes
{
    public class HeaderVersioningScheme : IVersioningScheme
    {
        public HeaderVersioningScheme(string headerName)
        {
            HeaderName = headerName;
        }

        public string HeaderName { get; }

        public void Apply(SelectorModel model, string version)
        {
            AttributeRouteModel attributeRouteModel = model.AttributeRouteModel;
            attributeRouteModel.Template = "api/" + attributeRouteModel.Template;
            model.ActionConstraints.Add(new Constraint(HeaderName, version));
        }

        private class Constraint : IActionConstraint
        {
            private readonly string _headerName;
            private readonly string _version;

            public Constraint(string headerName, string version)
            {
                _headerName = headerName;
                _version = version;
            }

            public bool Accept(ActionConstraintContext context)
            {
                return context.RouteContext.HttpContext.Request.Headers[_headerName] == _version;
            }

            public int Order { get; } = 100;
        }
    }
}
