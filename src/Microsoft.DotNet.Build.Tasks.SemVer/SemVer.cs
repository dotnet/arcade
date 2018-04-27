// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.SemVer
{
    public class SemVer : Task
    {
        private Dictionary<string, string> formats = new Dictionary<string, string> {
                                    {"dev", "{major}.{minor}.{patch}-{prerelease}.{shortdate}.{builds}+{shortsha}"},
                                    {"stable", "{major}.{minor}.{patch}-{prerelease}"},
                                    {"final", "{major}.{minor}.{patch}"}};

        [Required]
        public string FormatString { get; set; }

        [Output]
        public String VersionString { get; set; }

        public UInt16 Major { get; set; }
        public UInt16 Minor { get; set; }
        public UInt16 Patch { get; set; }
        public string Prerelease { get; set; }
        public string ShortDate { get; set; }
        public UInt16 Builds { get; set; }
        public string ShortSHA { get; set; }

        public override bool Execute()
        {
            VersionString = FormatString.ToLower();

            if (formats.ContainsKey(VersionString))
            {
                VersionString = formats[VersionString];
            }

            if ((VersionString.Contains("{major}") && Major == 0) ||
                (VersionString.Contains("{prerelease}") && String.IsNullOrEmpty(Prerelease)) ||
                (VersionString.Contains("{shortdate}") && String.IsNullOrEmpty(ShortDate)) ||
                (VersionString.Contains("{shortsha}") && String.IsNullOrEmpty(ShortSHA)))
            {
                Log.LogError("Invalid format string or parameters. All parameters referenced in the format string must be informed.");
                return false;
            }

            VersionString = VersionString.Replace("{major}", Major.ToString());
            VersionString = VersionString.Replace("{minor}", Minor.ToString());
            VersionString = VersionString.Replace("{patch}", Patch.ToString());
            VersionString = VersionString.Replace("{prerelease}", Prerelease);
            VersionString = VersionString.Replace("{shortdate}", ShortDate);
            VersionString = VersionString.Replace("{builds}", Builds.ToString());
            VersionString = VersionString.Replace("{shortsha}", ShortSHA);

            return true;
        }
    }
}
