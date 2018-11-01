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
    ///     - https://github.com/someorg/somerepo
    ///     - https://dev.azure.com/someaccount/someproject/_git/somerepo
    ///     While this is simple enough, there are other forms of 'dev.azure.com' repository URL that
    ///     also appear:
    ///     - https://dnceng.visualstudio.com/someproject/_git/somerepo
    ///     - https://dnceng@dev.azure.com/dnceng/someproject/_git/somerepo
    ///     If a user creates new subscriptions using a variety of these URLS the same target repo,
    ///     the repository level merge policies will diverge, and batched merges may not occur
    ///     as desired.
    ///     We could go two ways with this:
    ///     - Canonicalize the input dev azure URL
    ///     - Validate that the input URL is of a correct form.
    ///     Rather than try to guess exactly what the user meant, this code validates that all input URLS
    ///     are either of the first or second form listed initially.
    /// </summary>
    public class RepositoryUrlAttribute : ValidationAttribute
    {
        /// <summary>
        ///     To add a new valid URL form, add an entry here
        /// </summary>
        private readonly List<Regex> validUrlForms = new List<Regex>()
        {
            new Regex(@"^https://github\.com/[a-zA-Z0-9]+/[a-zA-Z0-9-]+$"),
            new Regex(@"^https://dev\.azure\.com/[a-zA-Z0-9]+/[a-zA-Z0-9-]+/_git/[a-zA-Z0-9-\.]+$")
        };

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            string targetUrl = (string)value;
            if (!validUrlForms.Any(form => form.IsMatch(targetUrl)))
            {
                string validForms = validUrlForms.Aggregate<Regex, string>("",
                    (curr, form) => $"{curr}\n{form.ToString()}");
                return new ValidationResult($"Target repository URL should be one of the following forms:{validForms}");
            }
            return ValidationResult.Success;
        }
    }
}
