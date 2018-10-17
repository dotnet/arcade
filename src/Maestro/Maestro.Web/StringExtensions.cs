// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Maestro.Web
{
    public static class StringExtensions
    {
        public static (string left, string right) Split2(this string value, char splitOn)
        {
            int idx = value.IndexOf(splitOn);

            if (idx < 0)
            {
                return (value, value.Substring(0, 0));
            }

            return (value.Substring(0, idx), value.Substring(idx + 1));
        }
    }
}
