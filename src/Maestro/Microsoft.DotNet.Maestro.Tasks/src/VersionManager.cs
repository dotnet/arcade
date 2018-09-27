// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.Maestro.Tasks
{
    public class VersionManager
    {
        public static string GetVersion(string assetName)
        {
            if (assetName.Contains('/'))
            {
                string[] pathSegments = assetName.Split('/');
                assetName = pathSegments[pathSegments.Length - 1];
            }

            string[] segments = assetName.Split('.');
            StringBuilder sb = new StringBuilder();
            int versionStart = 0;
            int versionEnd = 0;

            for (int i = 1; i < segments.Length; i++)
            {
                if (IsMajorAndMinor(segments[i - 1], segments[i]))
                {
                    versionStart = i - 1;
                    i++;

                    // Once we have a major and minor we continue to check all the segments and if any have digits in it versionEnd
                    // is updated. So far, produced assets don't have an extension with digits in it, if that changes we'd need to update
                    // this logic
                    while (i < segments.Length)
                    {
                        if (IsValidSegment(segments[i]))
                        {
                            versionEnd = i;
                        }

                        i++;
                    }
                }
            }

            if (versionStart == versionEnd)
            {
                return null;
            }

            // Append major which might cointain fragments of the name so we need to only get the numeric piece out of that
            string major = GetMajor(segments[versionStart++]);
            sb.Append($"{major}.");

            while (versionStart < versionEnd)
            {
                sb.Append($"{segments[versionStart++]}.");
            }

            sb.Append($"{segments[versionEnd]}");

            return sb.ToString();
        }

        private static bool IsMajorAndMinor(string major, string minor)
        {
            return major.Any(char.IsDigit) && int.TryParse(minor, out int min);
        }

        private static string GetMajor(string versionSegment)
        {
            if (int.TryParse(versionSegment, out int v))
            {
                return versionSegment;
            }

            int index = versionSegment.Length - 1;
            List<char> version = new List<char>();

            while (index > 0 && char.IsDigit(versionSegment[index]))
            {
                version.Insert(0, versionSegment[index--]);
            }

            return new string(version.ToArray());
        }

        private static bool IsValidSegment(string versionSegment)
        {
            HashSet<string> knownTags = new HashSet<string>
            {
                "alpha",
                "beta",
                "preview",
                "prerelease",
                "servicing"
            };

            return versionSegment.Any(char.IsDigit) || knownTags.Any(t => versionSegment.Contains(t));
        }
    }
}
