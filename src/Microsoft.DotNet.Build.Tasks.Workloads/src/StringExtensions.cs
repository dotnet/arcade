// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    public static class StringExtensions
    {
        public static string TrimStart(this string str, string trimString)
        {
            return str.StartsWith(trimString) ? str.Remove(0, trimString.Length) : str;
        }

        public static string TrimStart(this string str, string trimString, StringComparison comparisonType)
        {
            return str.StartsWith(trimString, comparisonType) ? str.Remove(0, trimString.Length) : str;
        }

        /// <summary>
        /// Replace multiple substrings using task items.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="replacementStrings">An array of task items containing substrings and replacement strings.</param>
        /// <returns></returns>
        public static string Replace(this string str, ITaskItem[] replacementStrings)
        {
            if ((replacementStrings is not null) && (replacementStrings.Length > 0))
            {
                foreach (ITaskItem item in replacementStrings)
                {
                    str = str.Replace(item.ItemSpec, item.GetMetadata(Metadata.Replacement));
                }
            }

            return str;
        }
    }
}
