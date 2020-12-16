// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public interface IPackageArtifactModelFactory
    {
        PackageArtifactModel CreatePackageArtifactModel(ITaskItem item);
    }

    public class PackageArtifactModelFactory : IPackageArtifactModelFactory
    {
        private readonly INupkgInfoFactory _nupkgInfoFactory;

        public PackageArtifactModelFactory(INupkgInfoFactory nupkgInfoFactory)
        {
            _nupkgInfoFactory = nupkgInfoFactory;
        }

        public PackageArtifactModel CreatePackageArtifactModel(ITaskItem item)
        {
            NupkgInfo info = _nupkgInfoFactory.CreateNupkgInfo(item.ItemSpec);

            return new PackageArtifactModel
            {
                Attributes = MSBuildListSplitter.GetNamedProperties(item.GetMetadata("ManifestArtifactData")),
                Id = info.Id,
                Version = info.Version
            };
        }
    }
}
