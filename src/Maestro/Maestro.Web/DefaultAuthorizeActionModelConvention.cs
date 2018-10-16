// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Maestro.Web
{
    public class DefaultAuthorizeActionModelConvention : IActionModelConvention
    {
        public DefaultAuthorizeActionModelConvention(string policyName)
        {
            Filter = new AuthorizeFilter(policyName);
        }

        public AuthorizeFilter Filter { get; }

        public void Apply(ActionModel action)
        {
            IEnumerable<IFilterMetadata> preexisting = action.Controller.Filters.Concat(action.Filters);
            if (preexisting.Any(f => f is IAsyncAuthorizationFilter || f is IAllowAnonymousFilter))
            {
                return;
            }

            IEnumerable<object> attributes = action.Controller.Attributes.Concat(action.Attributes);
            if (attributes.Any(a => a is IAllowAnonymous || a is IAuthorizeData))
            {
                return;
            }

            action.Filters.Add(Filter);
        }
    }
}
