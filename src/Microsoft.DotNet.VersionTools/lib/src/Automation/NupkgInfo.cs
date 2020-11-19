// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Microsoft.DotNet.VersionTools.Automation
{
    public class NupkgInfo
    {
        public NupkgInfo()
        {
        }

        public NupkgInfo(string path)
        {
            Initialize(path);
        }

        public virtual void Initialize(string path)
        {
            using (PackageArchiveReader archiveReader = new PackageArchiveReader(path))
            {
                PackageIdentity identity = archiveReader.GetIdentity();
                Id = identity.Id;
                Version = identity.Version.ToString();
                Prerelease = identity.Version.Release;
            }
        }

        public string Id { get; protected set; }
        public string Version { get; protected set; }
        public string Prerelease { get; protected set; }

        public static bool IsSymbolPackagePath(string path) => path.EndsWith(".symbols.nupkg");

        public static ServiceProvider GetDefaultProvider()
        {
            ServiceProvider defaultProvider = new ServiceCollection()
                .AddLogging()
                .AddSingleton<NupkgInfo>()
                .BuildServiceProvider();

            var test = defaultProvider.GetService<NupkgInfo>();

            return defaultProvider;
        }
    }
}
