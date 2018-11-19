// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Maestro.Web
{
    /// <summary>
    ///     Validates a repository url. This is targeted at the target repository url of a subscription.
    ///     These generally come in two desired forms at the moment:
    ///     <list type="bullet">
    ///         <item>https://github.com/someorg/somerepo</item>
    ///         <item>https://dev.azure.com/someaccount/someproject/_git/somerepo</item>
    ///     </list>
    ///     While this is simple enough, there are other forms of 'dev.azure.com' repository URL that
    ///     also appear:
    ///     <list type="bullet">
    ///         <item>https://dnceng.visualstudio.com/someproject/_git/somerepo</item>
    ///         <item>https://dnceng@dev.azure.com/dnceng/someproject/_git/somerepo</item>
    ///     </list>
    ///     If a user creates new subscriptions using a variety of these URLS the same target repo,
    ///     the repository level merge policies will diverge, and batched merges may not occur
    ///     as desired.
    ///     We could go two ways with this:
    ///     <list type="bullet">
    ///         <item>Canonicalize the input dev azure URL</item>
    ///         <item>Validate that the input URL is of a correct form.</item>
    ///     </list>
    ///     Rather than try to guess exactly what the user meant, this code validates that all input URLS
    ///     are either of the first or second form listed initially.
    /// </summary>
    public class RepositoryUrlAttribute : ValidationAttribute
    {
        /// <summary>
        ///     To add a new valid URL form, add an entry here
        /// </summary>
        private static readonly List<Regex> _validUrlForms = new List<Regex>()
        {
            new Regex(@"^https://github\.com/[a-zA-Z0-9]+/[a-zA-Z0-9-]+$"),
            new Regex(@"^https://dev\.azure\.com/[a-zA-Z0-9]+/[a-zA-Z0-9-]+/_git/[a-zA-Z0-9-\.]+$")
        };

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            string targetUrl = (string)value;
            if (!_validUrlForms.Any(form => form.IsMatch(targetUrl)))
            {
                return new ValidationResult("Target repository URL should be one of the following forms: " +
                    "https://github.com/:org/:repo or https://dev.azure.com/:account/:project/_git/:repo");
            }
            return ValidationResult.Success;
        }
    }
}
