// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.SemVer
{
    public class SemVer : Task
    {
        [Required]
        public Int16 Major { get; set; }
        [Required]
        public Int16 Minor { get; set; }
        [Required]
        public Int16 Patch { get; set; }

        public string Prerelease { get; set; }
        public Int16 ShortDate { get; set; }
        public Int16 Builds { get; set; }
        public string ShortSHA { get; set; }

        [Output]
        public String Version { get; set; }

        public override bool Execute()
        {
            if (Major <= 0)
            {
                Log.LogError("Major version cannot be zero.");
                return false;
            }

            // If Prerelease isn't specified then any of the other prerelease fields shouldn't
            if (String.IsNullOrEmpty(Prerelease))
            {
                if (ShortDate > 0 || Builds > 0 || !String.IsNullOrEmpty(ShortSHA))
                {
                    Log.LogError("Invalid prerelease parameters.");
                    return false;
                }

                Version = $"{Major}.{Minor}.{Patch} (stabilized)";
                return true;
            }

            // So far we know that these first four fields aren't empty
            Version = $"{Major}.{Minor}.{Patch}-{Prerelease}.{ShortDate:00000}.{Builds}+{ShortSHA}";

            return true;
        }
    }
}
