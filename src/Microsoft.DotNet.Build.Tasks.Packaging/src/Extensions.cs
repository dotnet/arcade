// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using NuGet;
using NuGet.Frameworks;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public static class Extensions
    {
        private static readonly NuGetFramework NullFramework = new NuGetFramework("Null,Version=v1.0");

        public static string GetString(this ITaskItem taskItem, string metadataName)
        {
            var metadataValue = taskItem.GetMetadata(metadataName)?.Trim();
            return String.IsNullOrEmpty(metadataValue) ? null : metadataValue;
        }

        public static bool GetBoolean(this ITaskItem taskItem, string metadataName, bool defaultValue = false)
        {
            bool result = false;
            var metadataValue = taskItem.GetMetadata(metadataName);
            if (!bool.TryParse(metadataValue, out result))
            {
                result = defaultValue;
            }
            return result;
        }

        public static NuGetFramework GetTargetFramework(this ITaskItem taskItem)
        {
            NuGetFramework result = null;
            var metadataValue = taskItem.GetMetadata(Metadata.TargetFramework);
            if (!string.IsNullOrEmpty(metadataValue))
            {
                result = NuGetFramework.Parse(metadataValue);
            }

            return result;
        }

        public static NuGetFramework GetTargetFrameworkMoniker(this ITaskItem taskItem)
        {
            NuGetFramework result = null;
            var metadataValue = taskItem.GetMetadata(Metadata.TargetFrameworkMoniker);
            if (!string.IsNullOrEmpty(metadataValue))
            {
                result = new NuGetFramework(metadataValue);
            }

            return result;
        }

        public static PackageDirectory GetPackageDirectory(this ITaskItem taskItem)
        {
            var packageDirectoryName = taskItem.GetMetadata(Metadata.PackageDirectory);
            if (string.IsNullOrEmpty(packageDirectoryName))
            {
                return PackageDirectory.Lib;
            }

            PackageDirectory result;
            Enum.TryParse(packageDirectoryName, true, out result);
            return result;
        }

        public static VersionRange GetVersion(this ITaskItem taskItem)
        {
            VersionRange result = null;
            var metadataValue = taskItem.GetMetadata(Metadata.Version);
            if (!string.IsNullOrEmpty(metadataValue))
            {
                VersionRange.TryParse(metadataValue, out result);
            }

            return result;
        }

        public static IReadOnlyList<string> GetValueList(this ITaskItem taskItem, string metadataName)
        {
            var metadataValue = taskItem.GetMetadata(metadataName);
            if (!string.IsNullOrEmpty(metadataValue))
            {
                return metadataValue.Split(';');
            }
            return null;
        }

        public static IEnumerable<string> GetStrings(this ITaskItem taskItem, string metadataName)
        {
            var metadataValue = taskItem.GetMetadata(metadataName)?.Trim();
            if (!string.IsNullOrEmpty(metadataValue))
            {
                return metadataValue.Split(';').Where(v => !String.IsNullOrEmpty(v.Trim())).ToArray();
            }

            return Enumerable.Empty<string>();
        }

        public static IEnumerable<T> NullAsEmpty<T>(this IEnumerable<T> source)
        {
            if (source == null)
            {
                return Enumerable.Empty<T>();
            }

            return source;
        }

        public static string ToStringSafe(this object value)
        {
            if (value == null)
            {
                return null;
            }

            return value.ToString();
        }

        public static void UpdateMember<T1, T2>(this T1 target, Expression<Func<T1, T2>> memberLamda, T2 value)
        {
            if (value == null)
            {
                return;
            }

            var memberSelectorExpression = memberLamda.Body as MemberExpression;
            if (memberSelectorExpression == null)
            {
                throw new InvalidOperationException("Invalid member expression.");
            }

            var property = memberSelectorExpression.Member as PropertyInfo;
            if (property == null)
            {
                throw new InvalidOperationException("Invalid member expression.");
            }

            property.SetValue(target, value, null);
        }

        public static void AddRangeToMember<T, TItem>(this T target, Expression<Func<T, ICollection<TItem>>> memberLamda, IEnumerable<TItem> value)
        {
            if (value == null || value.Count() == 0)
            {
                return;
            }

            var memberSelectorExpression = memberLamda.Body as MemberExpression;
            if (memberSelectorExpression == null)
            {
                throw new InvalidOperationException("Invalid member expression.");
            }

            var property = memberSelectorExpression.Member as PropertyInfo;
            if (property == null)
            {
                throw new InvalidOperationException("Invalid member expression.");
            }

            var list = (List<TItem>)property.GetValue(target) ?? new List<TItem>();
            list.AddRange(value);

            //property.SetValue(target, list, null);
        }
    }
}

namespace NuGet
{
    internal static class NuGetFrameworkExtensions
    { 
        // NuGet.Frameworks doesn't have the equivalent of the old VersionUtility.GetFrameworkString
        // which is relevant for building packages
        public static string GetFrameworkString(this NuGetFramework self)
        {
            var frameworkName = new FrameworkName(self.DotNetFrameworkName);
            string name = frameworkName.Identifier + frameworkName.Version;
            if (string.IsNullOrEmpty(frameworkName.Profile))
            {
                return name;
            }
            return name + "-" + frameworkName.Profile;
        }
    }
}
