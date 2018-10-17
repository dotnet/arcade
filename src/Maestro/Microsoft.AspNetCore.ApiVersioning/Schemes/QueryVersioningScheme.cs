// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Microsoft.AspNetCore.ApiVersioning.Schemes
{
    public class QueryVersioningScheme : IVersioningScheme
    {
        public QueryVersioningScheme(string parameterName)
        {
            ParameterName = parameterName;
        }

        public string ParameterName { get; }

        public void Apply(SelectorModel model, string version)
        {
            AttributeRouteModel attributeRouteModel = model.AttributeRouteModel;
            attributeRouteModel.Template = "api/" + attributeRouteModel.Template;
            model.ActionConstraints.Add(new Constraint(ParameterName, version));
        }

        private class Constraint : IActionConstraint
        {
            private readonly string _parameterName;
            private readonly string _version;

            public Constraint(string parameterName, string version)
            {
                _parameterName = parameterName;
                _version = version;
            }

            public bool Accept(ActionConstraintContext context)
            {
                IQueryCollection query = context.RouteContext.HttpContext.Request.Query;
                return query[_parameterName].ToString() == _version;
            }

            public int Order { get; } = 100;
        }
    }
}
