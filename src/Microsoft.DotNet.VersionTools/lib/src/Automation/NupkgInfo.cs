// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class NupkgInfo
    {
        private readonly PackageArchiveReaderFactory _packageArchiveReaderFactory;

        public NupkgInfo(PackageArchiveReaderFactory packageArchiveReaderFactory, string path)
        {
            _packageArchiveReaderFactory = packageArchiveReaderFactory;

            using (PackageArchiveReader archiveReader = _packageArchiveReaderFactory.CreatePackageArchiveReader(path))
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

    public interface IPackageArchiveReaderFactory
    {
        PackageArchiveReader CreatePackageArchiveReader(string path);
    }

    public class PackageArchiveReaderFactory : IPackageArchiveReaderFactory
    {
        public PackageArchiveReader CreatePackageArchiveReader(string path)
        {
            return new PackageArchiveReader(path);
        }
    }
}
