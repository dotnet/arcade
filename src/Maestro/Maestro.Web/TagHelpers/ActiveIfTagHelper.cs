// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Maestro.Web.TagHelpers
{
    // You may need to install the Microsoft.AspNetCore.Razor.Runtime package into your project
    [HtmlTargetElement(Attributes = "[active-if-page]")]
    public class ActiveIfTagHelper : TagHelper
    {
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        public string ActiveIfPage { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (ShouldAddActive())
            {
                TagHelperAttributeList attrs = output.Attributes;
                attrs.RemoveAll("active-if-route");
                attrs.Add("class", "active");
            }
        }

        private bool ShouldAddActive()
        {
            if (!string.IsNullOrEmpty(ActiveIfPage))
            {
                return ViewContext.IsCurrentPage(ActiveIfPage);
            }

            return false;
        }
    }
}
