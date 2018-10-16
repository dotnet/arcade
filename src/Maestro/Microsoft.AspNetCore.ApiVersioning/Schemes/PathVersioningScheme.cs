// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
