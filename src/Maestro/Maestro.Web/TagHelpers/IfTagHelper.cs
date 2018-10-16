// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Maestro.Web.TagHelpers
{
    [HtmlTargetElement(Attributes = "[if]")]
    [HtmlTargetElement(Attributes = "[if-not]")]
    [HtmlTargetElement(Attributes = "[if-page]")]
    public class IfTagHelper : TagHelper
    {
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        public bool If { get; set; }

        public bool? IfNot { get; set; }

        public string IfPage { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (!ShouldShowTag())
            {
                output.SuppressOutput();
            }
        }

        private bool ShouldShowTag()
        {
            if (!string.IsNullOrEmpty(IfPage))
            {
                return ViewContext.IsCurrentPage(IfPage);
            }

            if (IfNot.HasValue)
            {
                return !IfNot.Value;
            }

            return If;
        }
    }
}
