// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public interface IBlobArtifactModelFactory
    {
        BlobArtifactModel CreateBlobArtifactModel(ITaskItem item, TaskLoggingHelper log);
    }

    public class BlobArtifactModelFactory : IBlobArtifactModelFactory
    {
        public BlobArtifactModel CreateBlobArtifactModel(ITaskItem item, TaskLoggingHelper log)
        {
            string path = item.GetMetadata("RelativeBlobPath");
            if (string.IsNullOrEmpty(path))
            {
                log.LogError($"Missing 'RelativeBlobPath' property on blob {item.ItemSpec}");
            }

            return new BlobArtifactModel
            {
                Attributes = MSBuildListSplitter.GetNamedProperties(item.GetMetadata("ManifestArtifactData")),
                Id = path
            };
        }
    }
}
