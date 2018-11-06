// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DarcLib
{
    /// <summary>
    ///     Generic helpers for dealing with version files.
    /// </summary>
    public static class VersionFiles
    {
        /// <summary>
        ///     Locations of the version files within a repository.
        /// </summary>
        public const string VersionDetailsXml = "eng/Version.Details.xml";
        public const string VersionProps = "eng/Versions.props";
        public const string GlobalJson = "global.json";
        public const string VersionPropsVersionElementSuffix = "PackageVersion";
        public const string VersionPropsAlternateVersionElementSuffix = "Version";
        public const string VersionPropsPackageElementSuffix = "Package";

        public static string GetVersionPropsPackageVersionElementName(string dependencyName)
        {
            return $"{dependencyName.Replace(".", string.Empty)}{VersionPropsVersionElementSuffix}";
        }
        /// <summary>
        /// Special temporary alternative package version element names.  This is used where the
        /// version props file already has "Version" instead of PackageVersion. Eventually this will
        /// be replaced by use of configuration in Versions.Details.xml
        /// </summary>
        /// <param name="dependencyName">Original name of dependency</param>
        /// <returns></returns>
        public static string GetVersionPropsAlternatePackageVersionElementName(string dependencyName)
        {
            return $"{dependencyName.Replace(".", string.Empty)}{VersionPropsAlternateVersionElementSuffix}";
        }

        public static string GetVersionPropsPackageElementName(string dependencyName)
        {
            return $"{dependencyName.Replace(".", string.Empty)}{VersionPropsPackageElementSuffix}";
        }

        public static string CalculateGlobalJsonElementName(string dependencyName)
        {
            return dependencyName;
        }
    }
}
