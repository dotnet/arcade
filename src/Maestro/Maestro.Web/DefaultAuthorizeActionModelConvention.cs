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
            var preexisting = action.Controller.Filters.Concat(action.Filters);
            if (preexisting.Any(f => f is IAsyncAuthorizationFilter || f is IAllowAnonymousFilter))
                return;
            var attributes = action.Controller.Attributes.Concat(action.Attributes);
            if (attributes.Any(a => a is IAllowAnonymous || a is IAuthorizeData))
                return;
            action.Filters.Add(Filter);
        }
    }
}