// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class NupkgInfo
    {
        public NupkgInfo(string path)
        {
            using (PackageArchiveReader archiveReader = new PackageArchiveReader(path))
            {
                PackageIdentity identity = archiveReader.GetIdentity();
                Id = identity.Id;
                Version = identity.Version.ToString();
                Prerelease = identity.Version.Release;
            }
        }

        public string Id { get; }
        public string Version { get; }
        public string Prerelease { get; }

        public static bool IsSymbolPackagePath(string path) => path.EndsWith(".symbols.nupkg");
    }
}
