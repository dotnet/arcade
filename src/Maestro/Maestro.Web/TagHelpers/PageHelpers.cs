using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;

namespace Maestro.Web.TagHelpers
{
    public static class PageHelpers
    {
        public static bool IsCurrentPage(this ViewContext context, string pageName)
        {
            string normalizedRouteValue = NormalizedRouteValue.GetNormalizedRouteValue(context, "page");
            string expectedPageValue = ViewEnginePath.CombinePath(normalizedRouteValue, pageName);
            RouteData routeData = context.HttpContext.GetRouteData();
            var page = (string) routeData.Values["page"];
            if (page == expectedPageValue)
            {
                return true;
            }

            return false;
        }
    }
}
