// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public interface IPackageArtifactModelFactory
    {
        PackageArtifactModel CreatePackageArtifactModel(ITaskItem item);
    }

    public class PackageArtifactModelFactory : IPackageArtifactModelFactory
    {
        private ServiceProvider _provider;

        public PackageArtifactModelFactory()
        {
            _provider = new ServiceCollection()
                .AddSingleton<IPackageArchiveReaderFactory, PackageArchiveReaderFactory>()
                .AddTransient<NupkgInfo>()
                .BuildServiceProvider();
        }

        public PackageArtifactModel CreatePackageArtifactModel(ITaskItem item)
        {
            NupkgInfo info = ActivatorUtilities.CreateInstance(_provider, typeof(NupkgInfo), item.ItemSpec) as NupkgInfo;

            return new PackageArtifactModel
            {
                Attributes = MSBuildListSplitter.GetNamedProperties(item.GetMetadata("ManifestArtifactData")),
                Id = info.Id,
                Version = info.Version
            };
        }
    }
}
