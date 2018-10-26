// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.Darc
{
    public class Constants
    {
        public const string SettingsFileName = "settings";
        public const int ErrorCode = 42;
        public const int SuccessCode = 0;
        public const int MaxPopupTries = 3;
        public static string DarcDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".darc");

        /// <summary>
        /// Available update frequencies for subscriptions.  Currently the enumeration values aren't available
        /// through the generated API client.  When/if they ever are, this can be removed.
        /// </summary>
        public static readonly List<string> AvailableFrequencies = new List<string>
        {
            "none",
            "everyDay",
            "everyBuild"
        };

        /// <summary>
        /// This maybe should be implemented in the API in the future, help info for the available merge policies.  For now,
        /// this is just generic help for availabel merge policies
        /// </summary>
        public static readonly List<string> AvailableMergePolicyYamlHelp = new List<string>
        {
            "Merge policies are an optional set of rules that, if satisfied, mean that an",
            "auto-update PR will be automatically merged. A PR is only merged automatically if policies",
            "exist and all are satisfied.",
            "In YAML, policies are specified in a list using the following form:",
            "- Name: <name of policy>",
            "  Properties:",
            "  - <property set>",
            "Each policy may have a set of required properties." +
            "See below for available merge policies:",
            "",
            "AllChecksSuccessful - All PR checks must be successful, potentially ignoring a specified set of checks.",
            "Checks might be ignored if they are unrelated to PR validation. The check name corresponds to the string that shows up",
            "in GitHub/Azure DevOps.",
            "YAML format:",
            "- Name: AllChecksSuccessful",
            "  Properties:",
            "    ignoreChecks:",
            "    - WIP",
            "    - license/cla",
            "    - <other check names>",
            "",
            "RequireChecks - Require that a specific set of checks pass",
            "The check name corresponds to the string that shows up in GitHub/Azure DevOps.",
            " YAML format:",
            "- Name: RequireChecks",
            "  Properties:",
            "    checks:",
            "    - MyCIValidation",
            "    - CI",
            "    - <other check names>",
            "",
            "NoExtraCommits - If additional non-bot commits appear in the PR, the PR should not be merged.",
            "YAML format:",
            "- Name: NoExtraCommits",
        };
    }
}
